using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher
{
    public partial class LauncherViewModel : ObservableObject
    {
        private readonly LauncherService _launcherService;
        private readonly SettingsService _settingsService;
        private readonly WindowManager _windowManager;
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
            WindowManager windowManager)
        {
            _launcherService = launcherService;
            _settingsService = settingsService;
            _windowManager = windowManager;
            
            LoadItemOrders();
            InitializeItems();
        }

        private void LoadItemOrders()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptMasterv6", "launcher_orders.json");
                if (File.Exists(appDataPath))
                {
                    var json = File.ReadAllText(appDataPath);
                    _itemOrders = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                }
            }
            catch
            {
                _itemOrders = new();
            }
        }

        public void MoveItem(LauncherItem source, LauncherItem target)
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
            
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptMasterv6", "launcher_orders.json");
                var dir = Path.GetDirectoryName(appDataPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir!);
                var json = JsonSerializer.Serialize(_itemOrders);
                File.WriteAllText(appDataPath, json);
            }
            catch { }

            UpdateFilter();
        }

        public void SelectCategory(string category)
        {
            CurrentCategory = category;
            UpdateFilter();
        }

        private async void InitializeItems()
        {
            await LoadDiscoveredItemsAsync();
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
        private void ExecuteItem(LauncherItem item)
        {
            try
            {
                if (item?.Action != null)
                {
                    item.Action.Invoke();
                }
                else if (!string.IsNullOrEmpty(item?.FilePath))
                {
                    var info = new ProcessStartInfo(item.FilePath) { UseShellExecute = true };
                    
                    if (_settingsService.Config.LauncherRunAsAdmin)
                    {
                        info.Verb = "runas";
                    }

                    Process.Start(info);
                }
                
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to execute launcher item", "LauncherViewModel.ExecuteItem");
            }
        }

        public Action? RequestClose { get; set; }
    }
}
