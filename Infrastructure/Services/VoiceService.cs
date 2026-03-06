using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services.Transcribers;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PromptMasterv6.Infrastructure.Services
{
    /// <summary>
    /// 语音服务
    /// 使用策略模式支持多种语音识别引擎
    /// </summary>
    public class VoiceService : IVoiceService, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private IVoiceTranscriber? _currentTranscriber;
        private VoiceProvider _currentProvider;

        public bool IsRecording => _currentTranscriber?.IsRecording ?? false;

        public event EventHandler<float>? OnAudioLevelChanged;
        public event EventHandler? OnRecordingStarted;
        public event EventHandler? OnRecordingStopped;
        public event EventHandler<string>? OnIntermediateResult;
        public event EventHandler<string>? OnFinalResult;
        public event EventHandler<Exception>? OnError;

        public VoiceService(ISettingsService settingsService, IHttpClientFactory httpClientFactory)
        {
            _settingsService = settingsService;
            _httpClientFactory = httpClientFactory;

            // 初始化转写器
            InitializeTranscriber();

            // 监听配置变化
            _settingsService.Config.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppConfig.VoiceProvider))
                {
                    InitializeTranscriber();
                }
            };
        }

        private void InitializeTranscriber()
        {
            var config = _settingsService.Config;

            // 如果引擎类型变化，重新创建转写器
            if (_currentTranscriber == null || _currentProvider != config.VoiceProvider)
            {
                // 清理旧的转写器
                if (_currentTranscriber != null)
                {
                    UnsubscribeTranscriberEvents(_currentTranscriber);
                    _currentTranscriber.Dispose();
                }

                // 创建新的转写器
                _currentTranscriber = config.VoiceProvider switch
                {
                    VoiceProvider.Xunfei => new XunfeiIatTranscriber(_settingsService),
                    _ => new OpenAICompatibleTranscriber(_settingsService)
                };

                _currentProvider = config.VoiceProvider;
                SubscribeTranscriberEvents(_currentTranscriber);

                LoggerService.Instance.LogInfo($"Voice transcriber initialized: {_currentTranscriber.Name}", "VoiceService.InitializeTranscriber");
            }
        }

        private void SubscribeTranscriberEvents(IVoiceTranscriber transcriber)
        {
            transcriber.OnAudioLevelChanged += (s, e) => OnAudioLevelChanged?.Invoke(s, e);
            transcriber.OnRecordingStarted += (s, e) => OnRecordingStarted?.Invoke(s, e);
            transcriber.OnRecordingStopped += (s, e) => OnRecordingStopped?.Invoke(s, e);
            transcriber.OnIntermediateResult += (s, e) => OnIntermediateResult?.Invoke(s, e);
            transcriber.OnFinalResult += (s, e) => OnFinalResult?.Invoke(s, e);
            transcriber.OnError += (s, e) => OnError?.Invoke(s, e);
        }

        private void UnsubscribeTranscriberEvents(IVoiceTranscriber transcriber)
        {
            transcriber.OnAudioLevelChanged -= (s, e) => OnAudioLevelChanged?.Invoke(s, e);
            transcriber.OnRecordingStarted -= (s, e) => OnRecordingStarted?.Invoke(s, e);
            transcriber.OnRecordingStopped -= (s, e) => OnRecordingStopped?.Invoke(s, e);
            transcriber.OnIntermediateResult -= (s, e) => OnIntermediateResult?.Invoke(s, e);
            transcriber.OnFinalResult -= (s, e) => OnFinalResult?.Invoke(s, e);
            transcriber.OnError -= (s, e) => OnError?.Invoke(s, e);
        }

        public void UpdateConfig(string baseUrl, string apiKey, string model)
        {
            _currentTranscriber?.UpdateConfig(baseUrl, apiKey, model);
        }

        public void StartRecording()
        {
            InitializeTranscriber(); // 确保使用最新的配置
            _currentTranscriber?.StartRecording();
        }

        public async Task<string> StopRecordingAndTranscribeAsync(IReadOnlyList<string>? hotwords = null)
        {
            if (_currentTranscriber == null)
                return string.Empty;

            return await _currentTranscriber.StopRecordingAndTranscribeAsync(hotwords);
        }

        public void CancelRecording()
        {
            _currentTranscriber?.CancelRecording();
        }

        public async Task<bool> IsConfiguredAsync()
        {
            if (_currentTranscriber == null)
                return false;

            return await _currentTranscriber.IsConfiguredAsync();
        }

        public string GetCurrentProviderName()
        {
            return _currentTranscriber?.Name ?? "未初始化";
        }

        public void Dispose()
        {
            if (_currentTranscriber != null)
            {
                UnsubscribeTranscriberEvents(_currentTranscriber);
                _currentTranscriber.Dispose();
                _currentTranscriber = null;
            }
        }
    }
}
