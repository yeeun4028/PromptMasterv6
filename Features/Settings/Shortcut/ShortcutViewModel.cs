using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly UpdateShortcutFeature.Handler _updateShortcutHandler;
        private readonly LoggerService _logger;

        [ObservableProperty] private string _fullWindowHotkey;
        [ObservableProperty] private string _screenshotTranslateHotkey;
        [ObservableProperty] private string _ocrHotkey;
        [ObservableProperty] private string _pinToScreenHotkey;

        public ShortcutViewModel(
            UpdateShortcutFeature.Handler updateShortcutHandler,
            LoggerService logger,
            SettingsService settingsService)
        {
            _updateShortcutHandler = updateShortcutHandler;
            _logger = logger;

            // 从配置加载初始值
            var config = settingsService.Config;
            _fullWindowHotkey = config.FullWindowHotkey;
            _screenshotTranslateHotkey = config.ScreenshotTranslateHotkey;
            _ocrHotkey = config.OcrHotkey;
            _pinToScreenHotkey = config.PinToScreenHotkey;
        }

        [RelayCommand]
        private async Task SaveShortcutSettings()
        {
            var command = new UpdateShortcutFeature.Command(
                FullWindowHotkey,
                ScreenshotTranslateHotkey,
                OcrHotkey,
                PinToScreenHotkey
            );

            var result = await _updateShortcutHandler.Handle(command);

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "ShortcutViewModel.SaveShortcutSettings");
                // 通知其他组件重新加载快捷键
                WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
                // TODO: 显示成功提示 (Toast)
            }
            else
            {
                _logger.LogWarning(result.Message, "ShortcutViewModel.SaveShortcutSettings");
                // TODO: 显示错误提示
            }
        }

        [RelayCommand]
        private void UpdateLauncherHotkey()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }
    }
}
