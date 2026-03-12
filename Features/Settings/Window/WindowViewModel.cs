using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly UpdateWindowSettingsFeature.Handler _updateWindowSettingsHandler;
        private readonly LoggerService _logger;

        [ObservableProperty] private bool _autoHide;

        public WindowViewModel(
            UpdateWindowSettingsFeature.Handler updateWindowSettingsHandler,
            LoggerService logger,
            SettingsService settingsService)
        {
            _updateWindowSettingsHandler = updateWindowSettingsHandler;
            _logger = logger;

            // 从配置加载初始值
            var config = settingsService.Config;
            _autoHide = config.AutoHide;
        }

        [RelayCommand]
        private async Task SaveWindowSettings()
        {
            var command = new UpdateWindowSettingsFeature.Command(AutoHide);

            var result = await _updateWindowSettingsHandler.Handle(command);

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "WindowViewModel.SaveWindowSettings");
                // TODO: 显示成功提示 (Toast)
            }
            else
            {
                _logger.LogWarning(result.Message, "WindowViewModel.SaveWindowSettings");
                // TODO: 显示错误提示
            }
        }
    }
}
