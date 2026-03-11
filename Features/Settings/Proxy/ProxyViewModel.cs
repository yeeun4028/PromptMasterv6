using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Proxy
{
    public partial class ProxyViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty] private string _proxyAddress;

        public ProxyViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            var config = _settingsService.Config;
            _proxyAddress = config.ProxyAddress;
        }

        partial void OnProxyAddressChanged(string value)
        {
            _settingsService.Config.ProxyAddress = value;
        }
    }
}
