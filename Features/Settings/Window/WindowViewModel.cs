using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public WindowViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }
    }
}
