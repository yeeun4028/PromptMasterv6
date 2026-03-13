using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;
        private bool _isLoading = true;

        [ObservableProperty] private string _fullWindowHotkey;
        [ObservableProperty] private string _screenshotTranslateHotkey;
        [ObservableProperty] private string _ocrHotkey;
        [ObservableProperty] private string _pinToScreenHotkey;

        partial void OnFullWindowHotkeyChanged(string value) => OnHotkeyChanged();
        partial void OnScreenshotTranslateHotkeyChanged(string value) => OnHotkeyChanged();
        partial void OnOcrHotkeyChanged(string value) => OnHotkeyChanged();
        partial void OnPinToScreenHotkeyChanged(string value) => OnHotkeyChanged();

        private async void OnHotkeyChanged()
        {
            if (_isLoading) return;
            await SaveShortcutSettings();
        }

        public ShortcutViewModel(
            IMediator mediator,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _mediator = mediator;
            _logger = logger;
            _dialogService = dialogService;

            var config = settingsService.Config;
            _fullWindowHotkey = config.FullWindowHotkey;
            _screenshotTranslateHotkey = config.ScreenshotTranslateHotkey;
            _ocrHotkey = config.OcrHotkey;
            _pinToScreenHotkey = config.PinToScreenHotkey;

            _isLoading = false;
        }

        [RelayCommand]
        private async Task SaveShortcutSettings()
        {
            var result = await _mediator.Send(new UpdateShortcutFeature.Command(
                FullWindowHotkey,
                ScreenshotTranslateHotkey,
                OcrHotkey,
                PinToScreenHotkey
            ));

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "ShortcutViewModel.SaveShortcutSettings");
                WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
                _dialogService.ShowToast(result.Message, "Success");
            }
            else
            {
                _logger.LogWarning(result.Message, "ShortcutViewModel.SaveShortcutSettings");
                _dialogService.ShowToast(result.Message, "Error");
            }
        }

        [RelayCommand]
        private void UpdateLauncherHotkey()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }
    }
}
