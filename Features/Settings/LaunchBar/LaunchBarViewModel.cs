using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public partial class LaunchBarViewModel : ObservableObject
    {
        private readonly AddLaunchBarItemFeature.Handler _addHandler;
        private readonly RemoveLaunchBarItemFeature.Handler _removeHandler;
        private readonly MoveLaunchBarItemFeature.Handler _moveHandler;
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public LaunchBarViewModel(
            SettingsService settingsService,
            AddLaunchBarItemFeature.Handler addHandler,
            RemoveLaunchBarItemFeature.Handler removeHandler,
            MoveLaunchBarItemFeature.Handler moveHandler)
        {
            _settingsService = settingsService;
            _addHandler = addHandler;
            _removeHandler = removeHandler;
            _moveHandler = moveHandler;
        }

        [RelayCommand]
        private void AddLaunchBarItem()
        {
            _addHandler.Handle(new AddLaunchBarItemFeature.Command(
                "#FF007ACC",
                LaunchBarActionType.BuiltIn,
                "ToggleWindow",
                "主界面"
            ));
        }

        [RelayCommand]
        private void RemoveLaunchBarItem(LaunchBarItem? item)
        {
            if (item != null)
            {
                _removeHandler.Handle(new RemoveLaunchBarItemFeature.Command(item));
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemUp(LaunchBarItem? item)
        {
            if (item != null)
            {
                _moveHandler.Handle(new MoveLaunchBarItemFeature.Command(item, MoveLaunchBarItemFeature.MoveDirection.Up));
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemDown(LaunchBarItem? item)
        {
            if (item != null)
            {
                _moveHandler.Handle(new MoveLaunchBarItemFeature.Command(item, MoveLaunchBarItemFeature.MoveDirection.Down));
            }
        }
    }
}
