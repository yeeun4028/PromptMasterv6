using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly HotkeyService _hotkeyService;

        public AppConfig Config => _settingsService.Config;

        public ShortcutViewModel(SettingsService settingsService, HotkeyService hotkeyService)
        {
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;
        }
    }
}
