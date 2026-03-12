using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Settings.Automation
{
    public partial class AutomationViewModel : ObservableObject
    {
        private readonly UpdateAutomationFeature.Handler _updateAutomationHandler;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;

        [ObservableProperty] private ObservableCollection<WebTarget> _webDirectTargets;
        [ObservableProperty] private string _defaultWebTargetName;
        [ObservableProperty] private bool _enableDoubleEnterSend;

        public AutomationViewModel(
            UpdateAutomationFeature.Handler updateAutomationHandler,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _updateAutomationHandler = updateAutomationHandler;
            _logger = logger;
            _dialogService = dialogService;

            // 从配置加载初始值
            var config = settingsService.Config;
            _webDirectTargets = config.WebDirectTargets;
            _defaultWebTargetName = config.DefaultWebTargetName;
            _enableDoubleEnterSend = config.EnableDoubleEnterSend;
        }

        [RelayCommand]
        private async Task SaveAutomationSettings()
        {
            var command = new UpdateAutomationFeature.Command(DefaultWebTargetName, EnableDoubleEnterSend);

            var result = await _updateAutomationHandler.Handle(command, CancellationToken.None);

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "AutomationViewModel.SaveAutomationSettings");
                _dialogService.ShowToast(result.Message, "Success");
            }
            else
            {
                _logger.LogWarning(result.Message, "AutomationViewModel.SaveAutomationSettings");
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}
