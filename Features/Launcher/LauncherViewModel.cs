using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Launcher.Execution;
using PromptMasterv6.Features.Launcher.InitializeLauncher;
using PromptMasterv6.Features.Launcher.ReorderLauncherItems;
using PromptMasterv6.Features.Launcher.SwitchLauncherCategory;
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
        private CancellationTokenSource? _switchCancellationTokenSource;

        private static readonly string[] CategoryCycle = { "Bookmark", "Application", "Tool" };

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

        [ObservableProperty]
        private bool isLoading = false;

        public AppConfig Config => _settingsService.Config;

        public LauncherViewModel(
            SettingsService settingsService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _mediator = mediator;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;

            var result = await _mediator.Send(new InitializeLauncherFeature.Command());

            if (result.Success && result.Items != null)
            {
                _itemOrders = result.ItemOrders ?? new Dictionary<string, int>();

                foreach (var item in result.Items)
                {
                    Items.Add(item);
                }

                await RefreshDisplayAsync();
            }

            IsLoading = false;
        }

        private async Task RefreshDisplayAsync()
        {
            _switchCancellationTokenSource?.Cancel();
            _switchCancellationTokenSource = new CancellationTokenSource();
            var ct = _switchCancellationTokenSource.Token;

            try
            {
                var result = await _mediator.Send(new SwitchLauncherCategoryFeature.Command(
                    Items.ToList(),
                    _itemOrders,
                    CurrentCategory,
                    Config.IsLauncherSinglePageDisplayEnabled), ct);

                if (!result.Success) return;

                ct.ThrowIfCancellationRequested();

                await ApplyDisplayResultAsync(result, ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ApplyDisplayResultAsync(SwitchLauncherCategoryFeature.Result result, CancellationToken ct)
        {
            if (Config.IsLauncherSinglePageDisplayEnabled)
            {
                FilteredItems.Clear();

                Bookmarks.Clear();
                Applications.Clear();
                Tools.Clear();

                if (result.Bookmarks != null)
                {
                    await LoadInBatchesAsync(Bookmarks, result.Bookmarks, 20, ct);
                }
                if (result.Applications != null)
                {
                    await LoadInBatchesAsync(Applications, result.Applications, 20, ct);
                }
                if (result.Tools != null)
                {
                    await LoadInBatchesAsync(Tools, result.Tools, 20, ct);
                }
            }
            else
            {
                Bookmarks.Clear();
                Applications.Clear();
                Tools.Clear();

                FilteredItems.Clear();

                if (result.FilteredItems != null)
                {
                    await LoadInBatchesAsync(FilteredItems, result.FilteredItems, 20, ct);
                }
            }
        }

        private async Task LoadInBatchesAsync(
            ObservableCollection<LauncherItem> targetCollection,
            List<LauncherItem> sourceItems,
            int batchSize,
            CancellationToken ct)
        {
            for (int i = 0; i < sourceItems.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                targetCollection.Add(sourceItems[i]);
                if ((i + 1) % batchSize == 0)
                {
                    await Task.Delay(15, ct);
                }
            }
        }

        public async void MoveItem(LauncherItem source, LauncherItem target)
        {
            if (source == null || target == null || source == target) return;

            var result = await _mediator.Send(new ReorderLauncherItemsFeature.Command(Items, source, target));

            if (result.Success && result.UpdatedOrders != null)
            {
                _itemOrders = result.UpdatedOrders;
                await RefreshDisplayAsync();
            }
        }

        public async void SelectCategory(string category)
        {
            CurrentCategory = category;
            await RefreshDisplayAsync();
        }

        [RelayCommand]
        private void CycleCategoryUp()
        {
            if (Config.IsLauncherSinglePageDisplayEnabled) return;

            var currentIndex = Array.IndexOf(CategoryCycle, CurrentCategory);
            var newIndex = (currentIndex - 1 + CategoryCycle.Length) % CategoryCycle.Length;
            SelectCategory(CategoryCycle[newIndex]);
        }

        [RelayCommand]
        private void CycleCategoryDown()
        {
            if (Config.IsLauncherSinglePageDisplayEnabled) return;

            var currentIndex = Array.IndexOf(CategoryCycle, CurrentCategory);
            var newIndex = (currentIndex + 1) % CategoryCycle.Length;
            SelectCategory(CategoryCycle[newIndex]);
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = RefreshDisplayAsync();
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
