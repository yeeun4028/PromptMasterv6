using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public partial class LaunchBarViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly SettingsService _settingsService;

        public AppConfig Config => _settingsService.Config;

        public LaunchBarViewModel(
            SettingsService settingsService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _mediator = mediator;

            Config.LaunchBarItems.CollectionChanged += OnLaunchBarItemsCollectionChanged;

            foreach (var item in Config.LaunchBarItems)
            {
                item.PropertyChanged += OnLaunchBarItemPropertyChanged;
            }
        }

        private void OnLaunchBarItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (LaunchBarItem item in e.NewItems)
                {
                    item.PropertyChanged += OnLaunchBarItemPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (LaunchBarItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnLaunchBarItemPropertyChanged;
                }
            }
        }

        private void OnLaunchBarItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private async Task AddLaunchBarItem()
        {
            await _mediator.Send(new AddLaunchBarItemFeature.Command(
                "#FF007ACC",
                LaunchBarActionType.BuiltIn,
                "ToggleWindow",
                "主界面"
            ));
        }

        [RelayCommand]
        private async Task RemoveLaunchBarItem(LaunchBarItem? item)
        {
            if (item != null)
            {
                await _mediator.Send(new RemoveLaunchBarItemFeature.Command(item));
            }
        }

        [RelayCommand]
        private async Task MoveLaunchBarItemUp(LaunchBarItem? item)
        {
            if (item != null)
            {
                await _mediator.Send(new MoveLaunchBarItemFeature.Command(item, MoveLaunchBarItemFeature.MoveDirection.Up));
            }
        }

        [RelayCommand]
        private async Task MoveLaunchBarItemDown(LaunchBarItem? item)
        {
            if (item != null)
            {
                await _mediator.Send(new MoveLaunchBarItemFeature.Command(item, MoveLaunchBarItemFeature.MoveDirection.Down));
            }
        }
    }
}
