using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;

        [ObservableProperty] private bool _autoHide;

        public WindowViewModel(
            IMediator mediator,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _mediator = mediator;
            _logger = logger;
            _dialogService = dialogService;

            var config = settingsService.Config;
            _autoHide = config.AutoHide;
        }

        [RelayCommand]
        private async Task SaveWindowSettings()
        {
            var result = await _mediator.Send(new UpdateWindowSettingsFeature.Command(AutoHide));

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
