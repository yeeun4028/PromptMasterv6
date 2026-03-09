using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using System;

namespace PromptMasterv6.Features.Settings;

public partial class SettingsContainerViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IWindowManager _windowManager;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool isSettingsOpen;
    [ObservableProperty] private int selectedSettingsTab;

    public AiModelsViewModel AiModelsVM { get; }
    public SyncViewModel SyncVM { get; }
    public LauncherSettingsViewModel LauncherSettingsVM { get; }

    public AppConfig Config => _settingsService.Config;
    public LocalSettings LocalConfig => _settingsService.LocalConfig;

    public SettingsContainerViewModel(
        ISettingsService settingsService,
        IWindowManager windowManager,
        MainViewModel mainViewModel,
        AiModelsViewModel aiModelsVM,
        SyncViewModel syncVM,
        LauncherSettingsViewModel launcherSettingsVM)
    {
        _settingsService = settingsService;
        _windowManager = windowManager;
        _mainViewModel = mainViewModel;

        AiModelsVM = aiModelsVM;
        SyncVM = syncVM;
        LauncherSettingsVM = launcherSettingsVM;

        SyncVM.SetMainViewModel(_mainViewModel);
    }

    [RelayCommand]
    private void SelectSettingsTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int tabIndex))
        {
            SelectedSettingsTab = tabIndex;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
        _settingsService.SaveConfig();
        _settingsService.SaveLocalConfig();
        _windowManager.CloseWindow(_mainViewModel);
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
