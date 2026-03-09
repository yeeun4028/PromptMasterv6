using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Launcher.Execution;
using PromptMasterv6.Features.Launcher.Orders;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher
{
    public partial class LauncherViewModel : ObservableObject
    {
        private readonly LauncherService _launcherService;
        private readonly SettingsService _settingsService;
        private readonly WindowManager _windowManager;
        private readonly IMediator _mediator;
        private Dictionary<string, int> _itemOrders = new();

        [ObservableProperty]
        private ObservableCollection<LauncherItem> items = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<LauncherItem> filteredItems = new();

        [ObservableProperty]
        private ObservableCollection<LauncherItem> bookmarks = new();

        [ObservableProperty]
        private ObservableCollection<LauncherItem> applications = new();

        [ObservableProperty]
        private ObservableCollection<LauncherItem> tools = new();

        [ObservableProperty]
        private string currentCategory = "Bookmark";

        public AppConfig Config => _settingsService.Config;

        public LauncherViewModel(
            LauncherService launcherService, 
            SettingsService settingsService,
            WindowManager windowManager,
            IMediator mediator)
        {
            _launcherService = launcherService;
            _settingsService = settingsService;
            _windowManager = windowManager;
            _mediator = mediator;
            
            InitializeItemsAsync();
        }

        private async void InitializeItemsAsync()
        {
            _itemOrders = await _mediator.Send(new GetLauncherOrdersQuery());
            
            await LoadDiscoveredItemsAsync();
            UpdateFilter();
        }

        public async void MoveItem(LauncherItem source, LauncherItem target)
        {
            if (source == null || target == null || source == target) return;

            var oldIndex = Items.IndexOf(source);
            var newIndex = Items.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0) return;

            Items.Move(oldIndex, newIndex);

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                item.DisplayOrder = i;
                
                var key = $"{item.Category}_{item.Title}";
                _itemOrders[key] = i;
            }
            
            await _mediator.Send(new SaveLauncherOrdersCommand(_itemOrders));

            UpdateFilter();
        }

        public void SelectCategory(string category)
        {
            CurrentCategory = category;
            UpdateFilter();
        }

        private async Task LoadDiscoveredItemsAsync()
        {
            try
            {
                var paths = _settingsService.Config.LauncherSearchPaths;
                if (paths == null || !paths.Any()) return;

                var discovered = await _launcherService.GetItemsAsync(paths);
                foreach (var item in discovered)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load discovered items: {ex.Message}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            foreach (var item in Items)
            {
                var key = $"{item.Category}_{item.Title}";
                if (_itemOrders.TryGetValue(key, out var order))
                {
                    item.DisplayOrder = order;
                }
                else
                {
                    item.DisplayOrder = int.MaxValue;
                }
            }

            if (Config.IsLauncherSinglePageDisplayEnabled)
            {
                Bookmarks = new ObservableCollection<LauncherItem>(Items.Where(i => i.Category == LauncherCategory.Bookmark).OrderBy(i => i.DisplayOrder));
                Applications = new ObservableCollection<LauncherItem>(Items.Where(i => i.Category == LauncherCategory.Application).OrderBy(i => i.DisplayOrder));
                Tools = new ObservableCollection<LauncherItem>(Items.Where(i => i.Category == LauncherCategory.Tool).OrderBy(i => i.DisplayOrder));
            }
            else
            {
                var enumCategory = CurrentCategory switch
                {
                    "Bookmark" => LauncherCategory.Bookmark,
                    "Application" => LauncherCategory.Application,
                    "Tool" => LauncherCategory.Tool,
                    _ => LauncherCategory.Bookmark
                };

                var filtered = Items.Where(i => i.Category == enumCategory).OrderBy(i => i.DisplayOrder).ToList();
                FilteredItems = new ObservableCollection<LauncherItem>(filtered);
            }
        }

        [RelayCommand]
        private async Task ExecuteItem(LauncherItem item)
        {
            if (item == null) return;

            await _mediator.Send(new ExecuteLauncherItemCommand(item, Config.LauncherRunAsAdmin));
            
            RequestClose?.Invoke();
        }

        public Action? RequestClose { get; set; }
    }
}
