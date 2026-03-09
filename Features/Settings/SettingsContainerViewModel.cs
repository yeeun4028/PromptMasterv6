using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using System;

namespace PromptMasterv6.Features.Settings;

public partial class SettingsContainerViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly WindowManager _windowManager;

    [ObservableProperty] private bool isSettingsOpen;
    [ObservableProperty] private int selectedSettingsTab;

    public AiModelsViewModel AiModelsVM { get; }
    public SyncViewModel SyncVM { get; }
    public LauncherSettingsViewModel LauncherSettingsVM { get; }

    public AppConfig Config => _settingsService.Config;
    public LocalSettings LocalConfig => _settingsService.LocalConfig;

    public SettingsContainerViewModel(
        SettingsService settingsService,
        WindowManager windowManager,
        AiModelsViewModel aiModelsVM,
        SyncViewModel syncVM,
        LauncherSettingsViewModel launcherSettingsVM)
    {
        _settingsService = settingsService;
        _windowManager = windowManager;

        AiModelsVM = aiModelsVM;
        SyncVM = syncVM;
        LauncherSettingsVM = launcherSettingsVM;
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
