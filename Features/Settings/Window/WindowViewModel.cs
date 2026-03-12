using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly UpdateWindowSettingsFeature.Handler _updateWindowSettingsHandler;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;

        [ObservableProperty] private bool _autoHide;

        public WindowViewModel(
            UpdateWindowSettingsFeature.Handler updateWindowSettingsHandler,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _updateWindowSettingsHandler = updateWindowSettingsHandler;
            _logger = logger;
            _dialogService = dialogService;

            // 从配置加载初始值
            var config = settingsService.Config;
            _autoHide = config.AutoHide;
        }

        [RelayCommand]
        private async Task SaveWindowSettings()
        {
            var command = new UpdateWindowSettingsFeature.Command(AutoHide);

            var result = await _updateWindowSettingsHandler.Handle(command, CancellationToken.None);

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "WindowViewModel.SaveWindowSettings");
                _dialogService.ShowToast(result.Message, "Success");
            }
            else
            {
                _logger.LogWarning(result.Message, "WindowViewModel.SaveWindowSettings");
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}
