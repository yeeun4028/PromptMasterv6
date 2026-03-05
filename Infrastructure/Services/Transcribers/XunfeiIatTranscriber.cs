using NAudio.CoreAudioApi;
using NAudio.Wave;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv5.Infrastructure.Services.Transcribers
{
    /// <summary>
    /// 讯飞语音听写器
    /// 使用 WebSocket 实现实时流式语音识别
    /// </summary>
    public class XunfeiIatTranscriber : IVoiceTranscriber
    {
        private readonly ISettingsService _settingsService;

        private WaveInEvent? _waveIn;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private StringBuilder _resultBuilder = new();
        private bool _isRecording;
        private int _frameStatus = 0; // 0=首帧, 1=中间帧, 2=尾帧
        private TaskCompletionSource<string>? _resultTcs; // 用于等待最终结果

        public bool IsRecording => _isRecording;
        public string Name => "讯飞语音听写";

        public event EventHandler<float>? OnAudioLevelChanged;
        public event EventHandler? OnRecordingStarted;
        public event EventHandler? OnRecordingStopped;
        public event EventHandler<string>? OnIntermediateResult;
        public event EventHandler<string>? OnFinalResult;
        public event EventHandler<Exception>? OnError;

        // Volume ducking
        private float _savedVolume = -1f;
        private bool _wasMuted = false;

        // 讯飞 API 配置
        private const string Host = "iat-api.xfyun.cn";
        private const string Path = "/v2/iat";
        private const string BaseUrl = $"wss://{Host}{Path}";

        public XunfeiIatTranscriber(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void UpdateConfig(string baseUrl, string apiKey, string model)
        {
            // Config is pulled from _settingsService directly
        }

        public async void StartRecording()
        {
            if (_isRecording) return;

            LoggerService.Instance.LogInfo("StartRecording called", "XunfeiIatTranscriber.StartRecording");

            try
            {
                var config = _settingsService.Config;

                // 检查配置
                if (string.IsNullOrWhiteSpace(config.XunfeiAppId) ||
                    string.IsNullOrWhiteSpace(config.XunfeiApiKey) ||
                    string.IsNullOrWhiteSpace(config.XunfeiApiSecret))
                {
                    LoggerService.Instance.LogInfo("Xunfei config missing - please fill AppID, API Key, API Secret", "XunfeiIatTranscriber.StartRecording");
                    OnError?.Invoke(this, new Exception("讯飞语音听写未配置，请先填写 AppID、API Key 和 API Secret"));
                    return;
                }

                LoggerService.Instance.LogInfo($"Xunfei config found - AppId: {config.XunfeiAppId}, ApiKey length: {config.XunfeiApiKey?.Length ?? 0}", "XunfeiIatTranscriber.StartRecording");

                _resultBuilder.Clear();
                _frameStatus = 0;
                _cts = new CancellationTokenSource();
                _resultTcs = new TaskCompletionSource<string>();

                // 建立 WebSocket 连接
                LoggerService.Instance.LogInfo("Generating auth URL...", "XunfeiIatTranscriber.StartRecording");
                var authUrl = GenerateAuthUrl(config.XunfeiApiKey!, config.XunfeiApiSecret!);
                LoggerService.Instance.LogInfo($"Auth URL generated (first 100 chars): {authUrl.Substring(0, Math.Min(100, authUrl.Length))}", "XunfeiIatTranscriber.StartRecording");
                
                _webSocket = new ClientWebSocket();
                _webSocket.Options.Proxy = null;
                LoggerService.Instance.LogInfo("Connecting to WebSocket...", "XunfeiIatTranscriber.StartRecording");
                await _webSocket.ConnectAsync(new Uri(authUrl), _cts.Token);
                LoggerService.Instance.LogInfo($"WebSocket connected, state: {_webSocket.State}", "XunfeiIatTranscriber.StartRecording");

                // 启动接收任务
                LoggerService.Instance.LogInfo("Starting receive loop...", "XunfeiIatTranscriber.StartRecording");
                _ = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);

                // Duck system volume if enabled
                if (config.VoiceDuckVolume)
                {
                    DuckSystemVolume();
                }

                // 启动音频录制
                _waveIn = new WaveInEvent();
                _waveIn.DeviceNumber = 0;
                _waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;
                _waveIn.StartRecording();

                _isRecording = true;
                OnRecordingStarted?.Invoke(this, EventArgs.Empty);
                LoggerService.Instance.LogInfo("Voice recording started (Xunfei)", "XunfeiIatTranscriber.StartRecording");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to start recording", "XunfeiIatTranscriber.StartRecording");
                CleanupResources();
                OnError?.Invoke(this, ex);
            }
        }

        private async void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_webSocket?.State != WebSocketState.Open || _cts?.IsCancellationRequested == true)
            {
                return;
            }

            try
            {
                // 计算音量电平
                float max = 0;
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i + 0]);
                    var sample32 = sample / 32768f;
                    if (sample32 < 0) sample32 = -sample32;
                    if (sample32 > max) max = sample32;
                }
                OnAudioLevelChanged?.Invoke(this, max);

                // 发送音频数据
                await SendAudioFrameAsync(e.Buffer, e.BytesRecorded, _frameStatus);

                // 首帧之后都是中间帧
                if (_frameStatus == 0)
                {
                    _frameStatus = 1;
                    LoggerService.Instance.LogInfo("First audio frame sent", "XunfeiIatTranscriber.WaveIn_DataAvailable");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Error sending audio frame", "XunfeiIatTranscriber.WaveIn_DataAvailable");
            }
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isRecording = false;
            _waveIn?.Dispose();
            _waveIn = null;

            // Restore system volume
            if (_settingsService.Config.VoiceDuckVolume)
            {
                RestoreSystemVolume();
            }

            OnRecordingStopped?.Invoke(this, EventArgs.Empty);
            LoggerService.Instance.LogInfo("Recording stopped", "XunfeiIatTranscriber.WaveIn_RecordingStopped");
        }

        private async Task SendAudioFrameAsync(byte[] buffer, int bytesRecorded, int status)
        {
            if (_webSocket?.State != WebSocketState.Open) return;

            var config = _settingsService.Config;

            // 转换为 PCM 原始数据（16-bit signed）
            var pcmData = new byte[bytesRecorded];
            Array.Copy(buffer, pcmData, bytesRecorded);

            var frame = new
            {
                common = new { app_id = config.XunfeiAppId },
                business = new
                {
                    language = "zh_cn",
                    domain = "iat",
                    accent = "mandarin",
                    vad_eos = config.XunfeiVadEos,
                    dwa = config.XunfeiEnableIntermediateResult ? "wpgs" : "",
                    ptt = config.XunfeiEnablePunctuation ? 1 : 0
                },
                data = new
                {
                    status = status,
                    format = "audio/L16;rate=16000",
                    encoding = "raw",
                    audio = Convert.ToBase64String(pcmData)
                }
            };

            var json = JsonSerializer.Serialize(frame);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts!.Token);
        }

        private async Task SendEndFrameAsync()
        {
            if (_webSocket?.State != WebSocketState.Open) return;

            _frameStatus = 2; // 尾帧
            var emptyBuffer = Array.Empty<byte>();
            await SendAudioFrameAsync(emptyBuffer, 0, 2);
            LoggerService.Instance.LogInfo("End frame sent", "XunfeiIatTranscriber.SendEndFrameAsync");
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[4096];

            LoggerService.Instance.LogInfo("Receive loop started", "XunfeiIatTranscriber.ReceiveLoop");

            try
            {
                while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        LoggerService.Instance.LogInfo($"Received: {json.Substring(0, Math.Min(200, json.Length))}", "XunfeiIatTranscriber.ReceiveLoop");
                        ProcessResponse(json);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        LoggerService.Instance.LogInfo("WebSocket close message received", "XunfeiIatTranscriber.ReceiveLoop");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LoggerService.Instance.LogInfo("Receive loop cancelled", "XunfeiIatTranscriber.ReceiveLoop");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Error in receive loop", "XunfeiIatTranscriber.ReceiveLoop");
                OnError?.Invoke(this, ex);
            }
        }

        private void ProcessResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 检查错误码
                if (root.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 0)
                {
                    var message = root.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "未知错误";
                    LoggerService.Instance.LogInfo($"Xunfei API error: {codeElement.GetInt32()} - {message}", "XunfeiIatTranscriber.ProcessResponse");
                    OnError?.Invoke(this, new Exception($"讯飞 API 错误: {codeElement.GetInt32()} - {message}"));
                    return;
                }

                // 解析识别结果
                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("result", out var resultElement))
                {
                    var text = ExtractText(resultElement);

                    // 检查是否是最终结果（ls=true 表示最后一帧）
                    var isLastSegment = resultElement.TryGetProperty("ls", out var lsElement) && lsElement.GetBoolean();

                    if (!string.IsNullOrEmpty(text))
                    {
                        LoggerService.Instance.LogInfo($"Recognized text: {text}, isLastSegment: {isLastSegment}", "XunfeiIatTranscriber.ProcessResponse");
                        
                        // 累积结果
                        _resultBuilder.Append(text);
                        
                        // 触发中间结果事件
                        OnIntermediateResult?.Invoke(this, text);
                    }

                    // 如果是最后一段，触发最终结果事件并设置结果
                    if (isLastSegment)
                    {
                        var finalResult = _resultBuilder.ToString();
                        LoggerService.Instance.LogInfo($"Final segment received, total result: {finalResult}", "XunfeiIatTranscriber.ProcessResponse");
                        OnFinalResult?.Invoke(this, finalResult);
                        
                        // 设置结果，让 StopRecordingAndTranscribeAsync 可以立即返回
                        _resultTcs?.TrySetResult(finalResult);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Error processing response", "XunfeiIatTranscriber.ProcessResponse");
            }
        }

        private string ExtractText(JsonElement resultElement)
        {
            var sb = new StringBuilder();

            if (resultElement.TryGetProperty("ws", out var wsElement))
            {
                foreach (var word in wsElement.EnumerateArray())
                {
                    if (word.TryGetProperty("cw", out var cwElement))
                    {
                        foreach (var cw in cwElement.EnumerateArray())
                        {
                            if (cw.TryGetProperty("w", out var wElement))
                            {
                                sb.Append(wElement.GetString());
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        public async Task<string> StopRecordingAndTranscribeAsync(IReadOnlyList<string>? hotwords = null)
        {
            if (!_isRecording) return _resultBuilder.ToString();

            LoggerService.Instance.LogInfo("StopRecordingAndTranscribeAsync called", "XunfeiIatTranscriber.StopRecordingAndTranscribeAsync");

            try
            {
                // 发送尾帧
                await SendEndFrameAsync();

                // 停止录音
                _waveIn?.StopRecording();

                // 等待录音停止
                while (_isRecording)
                {
                    await Task.Delay(50);
                }

                // 使用 TaskCompletionSource 等待最终结果
                // 这样可以立即响应，不需要轮询
                LoggerService.Instance.LogInfo("Waiting for final result...", "XunfeiIatTranscriber.StopRecordingAndTranscribeAsync");
                
                var resultTask = _resultTcs?.Task ?? Task.FromResult(_resultBuilder.ToString());
                
                // 等待结果或超时（最多 2 秒）
                var completedTask = await Task.WhenAny(resultTask, Task.Delay(2000));
                
                string result;
                if (completedTask == resultTask)
                {
                    result = await resultTask;
                    LoggerService.Instance.LogInfo($"Result received immediately: {result}", "XunfeiIatTranscriber.StopRecordingAndTranscribeAsync");
                }
                else
                {
                    result = _resultBuilder.ToString();
                    LoggerService.Instance.LogInfo($"Timeout, using current result: {result}", "XunfeiIatTranscriber.StopRecordingAndTranscribeAsync");
                }

                // 触发后台清理任务，不阻塞当前返回，消除 1 秒延迟
                Task.Run(async () =>
                {
                    try
                    {
                        if (_webSocket?.State == WebSocketState.Open)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                        }
                    }
                    catch { }
                    finally
                    {
                        CleanupResources();
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Error stopping recording", "XunfeiIatTranscriber.StopRecordingAndTranscribeAsync");
                OnError?.Invoke(this, ex);
                CleanupResources();
                return _resultBuilder.ToString();
            }
        }

        public void CancelRecording()
        {
            try
            {
                _cts?.Cancel();
                _waveIn?.StopRecording();

                if (_webSocket?.State == WebSocketState.Open)
                {
                    _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cancelled", CancellationToken.None);
                }
            }
            catch { }

            CleanupResources();
        }

        private string GenerateAuthUrl(string apiKey, string apiSecret)
        {
            // RFC1123 日期格式
            var date = DateTime.UtcNow.ToString("r");

            // 拼接签名原文
            var signatureOrigin = $"host: {Host}\ndate: {date}\nGET {Path} HTTP/1.1";

            // HMAC-SHA256 签名
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin)));

            // 拼接 authorization
            var authorizationOrigin = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signature}\"";
            var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationOrigin));

            // 构建完整 URL
            return $"{BaseUrl}?authorization={Uri.EscapeDataString(authorization)}&date={Uri.EscapeDataString(date)}&host={Host}";
        }

        private void DuckSystemVolume()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _savedVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                _wasMuted = device.AudioEndpointVolume.Mute;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = _savedVolume * 0.1f;
                LoggerService.Instance.LogInfo($"Volume ducked: {_savedVolume:P0} -> {_savedVolume * 0.1f:P0}", "XunfeiIatTranscriber.DuckSystemVolume");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to duck volume", "XunfeiIatTranscriber.DuckSystemVolume");
            }
        }

        private void RestoreSystemVolume()
        {
            try
            {
                if (_savedVolume < 0) return;
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                device.AudioEndpointVolume.MasterVolumeLevelScalar = _savedVolume;
                device.AudioEndpointVolume.Mute = _wasMuted;
                LoggerService.Instance.LogInfo($"Volume restored: {_savedVolume:P0}", "XunfeiIatTranscriber.RestoreSystemVolume");
                _savedVolume = -1f;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to restore volume", "XunfeiIatTranscriber.RestoreSystemVolume");
            }
        }

        private void CleanupResources()
        {
            // 先取消所有正在运行的任务
            try { _cts?.Cancel(); } catch { }

            _waveIn?.Dispose();
            _waveIn = null;
            _webSocket?.Dispose();
            _webSocket = null;
            _cts?.Dispose();
            _cts = null;
            _isRecording = false;
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var config = _settingsService.Config;
            return !string.IsNullOrWhiteSpace(config.XunfeiAppId) &&
                   !string.IsNullOrWhiteSpace(config.XunfeiApiKey) &&
                   !string.IsNullOrWhiteSpace(config.XunfeiApiSecret);
        }

        public void Dispose()
        {
            CleanupResources();
        }
    }
}
