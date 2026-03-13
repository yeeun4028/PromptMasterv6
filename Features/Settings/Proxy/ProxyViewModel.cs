using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Proxy
{
    public partial class ProxyViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;
        private readonly DialogService _dialogService;

        [ObservableProperty] private string _proxyAddress;

        public ProxyViewModel(
            IMediator mediator,
            LoggerService logger,
            SettingsService settingsService,
            DialogService dialogService)
        {
            _mediator = mediator;
            _logger = logger;
            _dialogService = dialogService;

            var config = settingsService.Config;
            _proxyAddress = config.ProxyAddress;
        }

        [RelayCommand]
        private async Task SaveProxySettings()
        {
            var result = await _mediator.Send(new UpdateProxyFeature.Command(ProxyAddress));

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "ProxyViewModel.SaveProxySettings");
                _dialogService.ShowToast(result.Message, "Success");
            }
            else
            {
                _logger.LogWarning(result.Message, "ProxyViewModel.SaveProxySettings");
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}
