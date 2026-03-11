using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window
{
    public partial class WindowViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty] private bool _autoHide;
        [ObservableProperty] private int _autoHideDelay;

        public WindowViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            var config = _settingsService.Config;
            _autoHide = config.AutoHide;
            _autoHideDelay = config.AutoHideDelay;
        }

        partial void OnAutoHideChanged(bool value)
        {
            _settingsService.Config.AutoHide = value;
        }

        partial void OnAutoHideDelayChanged(int value)
        {
            _settingsService.Config.AutoHideDelay = value;
        }
    }
}
