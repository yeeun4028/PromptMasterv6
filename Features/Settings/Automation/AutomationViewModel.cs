using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Settings.Automation
{
    public partial class AutomationViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;

        [ObservableProperty] private ObservableCollection<WebTarget> _webDirectTargets;
        [ObservableProperty] private string _defaultWebTargetName;
        [ObservableProperty] private bool _enableDoubleEnterSend;

        public AutomationViewModel(
            IMediator mediator,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _mediator = mediator;
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

            var result = await _mediator.Send(command);

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
