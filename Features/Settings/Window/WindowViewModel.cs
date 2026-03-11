using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty] private bool _autoHide;

        public WindowViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            var config = _settingsService.Config;
            _autoHide = config.AutoHide;
        }

        partial void OnAutoHideChanged(bool value)
        {
            _settingsService.Config.AutoHide = value;
        }
    }
}
