using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Infrastructure.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PromptMasterv5.ViewModels
{
    public partial class VoiceControlViewModel : ObservableObject
    {
        private readonly IVoiceService _voiceService;
        private readonly ICommandExecutionService _commandExecutionService;

        [ObservableProperty] private string statusText = "Listening...";
        [ObservableProperty] private bool isListening = false;
        [ObservableProperty] private bool isProcessing = false;
        [ObservableProperty] private float audioLevel = 0.0f;
        [ObservableProperty] private string recognizedText = "";

        public Action? RequestClose;

        public VoiceControlViewModel(IVoiceService voiceService, ICommandExecutionService commandExecutionService)
        {
            _voiceService = voiceService;
            _commandExecutionService = commandExecutionService;

            _voiceService.OnAudioLevelChanged += VoiceService_OnAudioLevelChanged;
            _voiceService.OnRecordingStarted += (s, e) => 
            {
                 IsListening = true; 
                 StatusText = "Listening...";
                 RecognizedText = "";
            };
            _voiceService.OnRecordingStopped += (s, e) => IsListening = false;
            
            // Auto-close timer if needed, but for now we rely on explicit stop or silence
        }

        private void VoiceService_OnAudioLevelChanged(object? sender, float level)
        {
            // Update UI on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() => AudioLevel = level * 100); 
        }

        public void StartRecordingSession()
        {
            StatusText = "Listening...";
            RecognizedText = "";
            IsProcessing = false;
            _voiceService.StartRecording();
        }

        public async Task StopAndProcess()
        {
            if (!IsListening) return;

            IsListening = false;
            IsProcessing = true;
            StatusText = "Processing...";

            try
            {
                var text = await _voiceService.StopRecordingAndTranscribeAsync();
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    StatusText = "Didn't catch that.";
                    await Task.Delay(1000);
                    RequestClose?.Invoke();
                    return;
                }

                RecognizedText = text;
                StatusText = "Executing...";

                bool result = _commandExecutionService.ExecuteCommand(text);
                
                if (!result)
                {
                    StatusText = $"Unknown command: {text}";
                }
                else
                {
                    StatusText = $"Active: {text}";
                }

                await Task.Delay(1500);
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                RecognizedText = ex.Message;
                LoggerService.Instance.LogException(ex, "Voice Session Error", "VoiceControlViewModel");
                await Task.Delay(2000);
                RequestClose?.Invoke();
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public void Cancel()
        {
            _voiceService.CancelRecording();
            RequestClose?.Invoke();
        }
    }
}
