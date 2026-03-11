using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.Proxy
{
    public partial class ProxyViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public ProxyViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }
    }
}
