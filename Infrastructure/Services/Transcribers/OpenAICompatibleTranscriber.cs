using NAudio.CoreAudioApi;
using NAudio.Wave;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptMasterv5.Infrastructure.Services.Transcribers
{
    /// <summary>
    /// OpenAI 兼容 API 语音转写器
    /// 支持 Whisper、硅基流动 SenseVoice 等兼容接口
    /// </summary>
    public class OpenAICompatibleTranscriber : IVoiceTranscriber
    {
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;

        private WaveInEvent? _waveIn;
        private WaveFileWriter? _waveWriter;
        private string _tempFilePath = "";

        private bool _isRecording;
        public bool IsRecording => _isRecording;

        public string Name => "OpenAI 兼容 API";

        public event EventHandler<float>? OnAudioLevelChanged;
        public event EventHandler? OnRecordingStarted;
        public event EventHandler? OnRecordingStopped;
#pragma warning disable CS0067
        public event EventHandler<string>? OnIntermediateResult;
#pragma warning restore CS0067
        public event EventHandler<string>? OnFinalResult;
        public event EventHandler<Exception>? OnError;

        private DateTime _lastAudioDetectedTime;
        private const float SilenceThreshold = 0.02f;
        private const int SilenceTimeoutMs = 600;

        // Volume ducking
        private float _savedVolume = -1f;
        private bool _wasMuted = false;

        public OpenAICompatibleTranscriber(ISettingsService settingsService, IHttpClientFactory httpClientFactory)
        {
            _settingsService = settingsService;
            _httpClientFactory = httpClientFactory;
        }

        public void UpdateConfig(string baseUrl, string apiKey, string model)
        {
            // Config is pulled from _settingsService directly
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            try
            {
                _waveIn = new WaveInEvent();
                _waveIn.DeviceNumber = 0;
                _waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;

                _tempFilePath = Path.Combine(Path.GetTempPath(), $"voice_cmd_{Guid.NewGuid()}.wav");
                _waveWriter = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);

                // Duck system volume if enabled
                if (_settingsService.Config.VoiceDuckVolume)
                {
                    DuckSystemVolume();
                }

                _waveIn.StartRecording();
                _isRecording = true;
                _lastAudioDetectedTime = DateTime.Now;

                OnRecordingStarted?.Invoke(this, EventArgs.Empty);
                LoggerService.Instance.LogInfo("Voice recording started (OpenAI)", "OpenAICompatibleTranscriber.StartRecording");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to start recording", "OpenAICompatibleTranscriber.StartRecording");
                CleanupResources();
                OnError?.Invoke(this, ex);
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_waveWriter == null) return;

            _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

            // Calculate peak volume for level meter
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i + 0]);
                var sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > max) max = sample32;
            }

            OnAudioLevelChanged?.Invoke(this, max);
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isRecording = false;
            _waveWriter?.Dispose();
            _waveWriter = null;
            _waveIn?.Dispose();
            _waveIn = null;

            // Restore system volume
            if (_settingsService.Config.VoiceDuckVolume)
            {
                RestoreSystemVolume();
            }

            OnRecordingStopped?.Invoke(this, EventArgs.Empty);
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
                LoggerService.Instance.LogInfo($"Volume ducked: {_savedVolume:P0} -> {_savedVolume * 0.1f:P0}", "OpenAICompatibleTranscriber.DuckSystemVolume");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to duck volume", "OpenAICompatibleTranscriber.DuckSystemVolume");
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
                LoggerService.Instance.LogInfo($"Volume restored: {_savedVolume:P0}", "OpenAICompatibleTranscriber.RestoreSystemVolume");
                _savedVolume = -1f;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to restore volume", "OpenAICompatibleTranscriber.RestoreSystemVolume");
            }
        }

        public async Task<string> StopRecordingAndTranscribeAsync(IReadOnlyList<string>? hotwords = null)
        {
            if (!_isRecording && string.IsNullOrEmpty(_tempFilePath)) return string.Empty;

            try
            {
                // Stop recording
                _waveIn?.StopRecording();

                // Wait briefly for the file to be fully written
                while (_isRecording)
                {
                    await Task.Delay(50);
                }

                if (!File.Exists(_tempFilePath)) return string.Empty;

                // Send to API
                var result = await TranscribeFileAsync(_tempFilePath, hotwords);
                OnFinalResult?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Error during stop and transcribe", "OpenAICompatibleTranscriber.StopRecordingAndTranscribeAsync");
                OnError?.Invoke(this, ex);
                return "";
            }
            finally
            {
                // Cleanup file
                try
                {
                    if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);
                }
                catch { }
                _tempFilePath = "";
            }
        }

        public void CancelRecording()
        {
            if (_isRecording)
            {
                _waveIn?.StopRecording();
            }
            // Cleanup file immediately
            try
            {
                if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);
            }
            catch { }
            _tempFilePath = "";
        }

        private async Task<string> TranscribeFileAsync(string filePath, IReadOnlyList<string>? hotwords = null)
        {
            try
            {
                var config = _settingsService.Config;
                string apiKey = config.VoiceApiKey;
                string baseUrl = config.VoiceApiBaseUrl;
                string model = config.VoiceApiModel;

                // Try to use centralized model config if VoiceModelId is set
                if (!string.IsNullOrEmpty(config.VoiceModelId))
                {
                    var selectedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.VoiceModelId);
                    if (selectedModel != null)
                    {
                        apiKey = selectedModel.ApiKey;
                        baseUrl = selectedModel.BaseUrl;
                        model = selectedModel.ModelName;
                    }
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new Exception("Voice API Key is not configured.");

                // Ensure BaseURL ends correctly
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                // Build request URL
                string requestUrl = baseUrl;
                if (!requestUrl.Contains("transcriptions"))
                {
                    requestUrl = new Uri(new Uri(baseUrl), "audio/transcriptions").ToString();
                }

                // 使用 IHttpClientFactory 创建 HttpClient
                var httpClient = _httpClientFactory.CreateClient("VoiceClient");

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var form = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var fileContent = new StreamContent(fileStream);

                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                form.Add(fileContent, "file", "audio.wav");
                form.Add(new StringContent(model), "model");

                // Add FunASR-style hotwords if available
                if (hotwords != null && hotwords.Count > 0)
                {
                    var hotwordStr = string.Join(" ", hotwords.Select(w => $"{w} 20"));
                    form.Add(new StringContent(hotwordStr), "hotword");
                    LoggerService.Instance.LogInfo($"Sending hotwords: {hotwordStr}", "OpenAICompatibleTranscriber.TranscribeFileAsync");
                }

                request.Content = form;

                var response = await httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API Error: {response.StatusCode} - {responseString}");
                }

                // Parse JSON response key "text"
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString()?.Trim() ?? "";
                }

                return "";
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Transcription failed", "OpenAICompatibleTranscriber.TranscribeFileAsync");
                throw;
            }
        }

        private void CleanupResources()
        {
            _waveWriter?.Dispose();
            _waveWriter = null;
            _waveIn?.Dispose();
            _waveIn = null;
            _isRecording = false;
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var config = _settingsService.Config;

            // Check if using saved model
            if (!string.IsNullOrEmpty(config.VoiceModelId))
            {
                var model = config.SavedModels.FirstOrDefault(m => m.Id == config.VoiceModelId);
                return model != null && !string.IsNullOrWhiteSpace(model.ApiKey);
            }

            // Check direct config
            return !string.IsNullOrWhiteSpace(config.VoiceApiKey);
        }

        public void Dispose()
        {
            CleanupResources();
        }
    }
}
