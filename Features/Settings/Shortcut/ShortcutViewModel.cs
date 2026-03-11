using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public ShortcutViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void UpdateWindowHotkeys()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        public void UpdateExternalToolsHotkeys()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        public void UpdateLauncherHotkey()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }
    }
}
