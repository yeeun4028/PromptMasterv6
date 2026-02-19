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

        [ObservableProperty] private string statusText = "";
        [ObservableProperty] private bool isListening = false;
        [ObservableProperty] private bool isProcessing = false;
        [ObservableProperty] private float audioLevel = 0.0f;
        [ObservableProperty] private string recognizedText = "";
        [ObservableProperty] private double audioScale = 0.2;

        public Action? RequestClose;

        public VoiceControlViewModel(IVoiceService voiceService, ICommandExecutionService commandExecutionService)
        {
            _voiceService = voiceService;
            _commandExecutionService = commandExecutionService;

            _voiceService.OnAudioLevelChanged += VoiceService_OnAudioLevelChanged;
            _voiceService.OnRecordingStarted += (s, e) => 
            {
                 IsListening = true; 
                 StatusText = ""; // Don't show "Listening..."
                 RecognizedText = "";
            };
            _voiceService.OnRecordingStopped += (s, e) => IsListening = false;
            
            // Auto-close timer if needed, but for now we rely on explicit stop or silence
        }

        [ObservableProperty] private double audioScaleBar1 = 0.2;
        [ObservableProperty] private double audioScaleBar2 = 0.2;
        [ObservableProperty] private double audioScaleBar3 = 0.2;
        [ObservableProperty] private double audioScaleBar4 = 0.2;
        [ObservableProperty] private double audioScaleBar5 = 0.2;

        private void VoiceService_OnAudioLevelChanged(object? sender, float level)
        {
            // Update UI on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                AudioLevel = level * 100;
                
                // Base scale for waveform (0.2 to 1.5)
                double baseScale = 0.2 + (level * 1.5);
                
                // Add some pseudo-randomness or wave pattern
                var random = new Random();
                
                // Center bar is loudest
                AudioScaleBar3 = Math.Min(2.0, baseScale * 1.2);
                
                // Side bars slightly lower
                AudioScaleBar2 = Math.Min(2.0, baseScale * 0.85); 
                AudioScaleBar4 = Math.Min(2.0, baseScale * 0.9);
                
                // Outer bars lowest
                AudioScaleBar1 = Math.Min(2.0, baseScale * 0.6); 
                AudioScaleBar5 = Math.Min(2.0, baseScale * 0.5);
            }); 
        }

        public void StartRecordingSession()
        {
            StatusText = "";
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
                var hotwords = _commandExecutionService.GetCommandKeys();
                var text = await _voiceService.StopRecordingAndTranscribeAsync(hotwords);
                
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
