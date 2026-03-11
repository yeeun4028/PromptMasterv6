using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Automation
{
    public partial class AutomationViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public AutomationViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }
    }
}
