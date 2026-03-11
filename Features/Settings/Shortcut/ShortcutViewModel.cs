using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty] private string _fullWindowHotkey;
        [ObservableProperty] private string _screenshotTranslateHotkey;
        [ObservableProperty] private string _ocrHotkey;
        [ObservableProperty] private string _pinToScreenHotkey;

        public ShortcutViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            var config = _settingsService.Config;
            _fullWindowHotkey = config.FullWindowHotkey;
            _screenshotTranslateHotkey = config.ScreenshotTranslateHotkey;
            _ocrHotkey = config.OcrHotkey;
            _pinToScreenHotkey = config.PinToScreenHotkey;
        }

        partial void OnFullWindowHotkeyChanged(string value)
        {
            _settingsService.Config.FullWindowHotkey = value;
            UpdateWindowHotkeys();
        }

        partial void OnScreenshotTranslateHotkeyChanged(string value)
        {
            _settingsService.Config.ScreenshotTranslateHotkey = value;
            UpdateExternalToolsHotkeys();
        }

        partial void OnOcrHotkeyChanged(string value)
        {
            _settingsService.Config.OcrHotkey = value;
            UpdateExternalToolsHotkeys();
        }

        partial void OnPinToScreenHotkeyChanged(string value)
        {
            _settingsService.Config.PinToScreenHotkey = value;
            UpdateExternalToolsHotkeys();
        }

        private void UpdateWindowHotkeys()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        private void UpdateExternalToolsHotkeys()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        [RelayCommand]
        private void UpdateLauncherHotkey()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }
    }
}
