using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public partial class LaunchBarViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public LaunchBarViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [RelayCommand]
        private void AddLaunchBarItem()
        {
            Config.LaunchBarItems.Add(new LaunchBarItem
            {
                ColorHex = "#FF007ACC",
                ActionType = LaunchBarActionType.BuiltIn,
                ActionTarget = "ToggleWindow",
                Label = "主界面"
            });
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void RemoveLaunchBarItem(LaunchBarItem? item)
        {
            if (item != null)
            {
                Config.LaunchBarItems.Remove(item);
                _settingsService.SaveConfig();
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemUp(LaunchBarItem? item)
        {
            if (item != null)
            {
                int index = Config.LaunchBarItems.IndexOf(item);
                if (index > 0)
                {
                    Config.LaunchBarItems.Move(index, index - 1);
                    _settingsService.SaveConfig();
                }
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemDown(LaunchBarItem? item)
        {
            if (item != null)
            {
                int index = Config.LaunchBarItems.IndexOf(item);
                if (index >= 0 && index < Config.LaunchBarItems.Count - 1)
                {
                    Config.LaunchBarItems.Move(index, index + 1);
                    _settingsService.SaveConfig();
                }
            }
        }
    }
}
