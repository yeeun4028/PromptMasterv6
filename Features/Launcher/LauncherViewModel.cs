using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Launcher.Execution;
using PromptMasterv6.Features.Launcher.Orders;
using PromptMasterv6.Features.Launcher.Queries;
using PromptMasterv6.Features.Launcher.ReorderLauncherItems;
using PromptMasterv6.Features.Launcher.FilterLauncherItems;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher
{
    public partial class LauncherViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IMediator _mediator;
        private Dictionary<string, int> _itemOrders = new();
        private CancellationTokenSource? _renderCancellationTokenSource;

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
            SettingsService settingsService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _mediator = mediator;

            InitializeItemsAsync();
        }

        private async void InitializeItemsAsync()
        {
            _itemOrders = await _mediator.Send(new GetLauncherOrdersQuery());

            await LoadDiscoveredItemsAsync();
            await UpdateFilterAsync();
        }

        public async void MoveItem(LauncherItem source, LauncherItem target)
        {
            if (source == null || target == null || source == target) return;

            var result = await _mediator.Send(new ReorderLauncherItemsFeature.Command(Items, source, target));

            if (result.Success && result.UpdatedOrders != null)
            {
                _itemOrders = result.UpdatedOrders;
                await UpdateFilterAsync();
            }
        }

        public async void SelectCategory(string category)
        {
            CurrentCategory = category;
            await UpdateFilterAsync();
        }

        private async Task LoadDiscoveredItemsAsync()
        {
            try
            {
                var paths = _settingsService.Config.LauncherSearchPaths;
                if (paths == null || !paths.Any()) return;

                var discovered = await _mediator.Send(new GetLauncherItemsQuery(paths));
                foreach (var item in discovered)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load discovered items: {ex.Message}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = UpdateFilterAsync();
        }

        private async Task UpdateFilterAsync()
        {
            _renderCancellationTokenSource?.Cancel();
            _renderCancellationTokenSource = new CancellationTokenSource();
            var token = _renderCancellationTokenSource.Token;

            try
            {
                var result = await _mediator.Send(new FilterLauncherItemsFeature.Command(
                    Items,
                    _itemOrders,
                    CurrentCategory,
                    Config.IsLauncherSinglePageDisplayEnabled));

                if (!result.Success) return;

                if (Config.IsLauncherSinglePageDisplayEnabled)
                {
                    token.ThrowIfCancellationRequested();

                    Bookmarks.Clear();
                    Applications.Clear();
                    Tools.Clear();

                    if (result.Bookmarks != null)
                    {
                        await LoadInBatchesAsync(Bookmarks, result.Bookmarks, 20, token);
                    }
                    if (result.Applications != null)
                    {
                        await LoadInBatchesAsync(Applications, result.Applications, 20, token);
                    }
                    if (result.Tools != null)
                    {
                        await LoadInBatchesAsync(Tools, result.Tools, 20, token);
                    }
                }
                else
                {
                    token.ThrowIfCancellationRequested();

                    FilteredItems.Clear();

                    if (result.FilteredItems != null)
                    {
                        await LoadInBatchesAsync(FilteredItems, result.FilteredItems, 20, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 取消时静默处理
            }
        }

        private async Task LoadInBatchesAsync(
            ObservableCollection<LauncherItem> targetCollection,
            List<LauncherItem> sourceItems,
            int batchSize,
            CancellationToken token)
        {
            for (int i = 0; i < sourceItems.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                targetCollection.Add(sourceItems[i]);

                if ((i + 1) % batchSize == 0)
                {
                    await Task.Delay(15, token);
                }
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
