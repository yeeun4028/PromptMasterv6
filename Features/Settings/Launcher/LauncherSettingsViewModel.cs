using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using System;
using System.Windows;

namespace PromptMasterv6.Features.Settings.Launcher;

public partial class LauncherSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public AppConfig Config => _settingsService.Config;

    public LauncherSettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void AddLauncherSearchPath()
    {
        try
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择要添加的搜索文件夹",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.SelectedPath;
                if (!Config.LauncherSearchPaths.Contains(path))
                {
                    Config.LauncherSearchPaths.Add(path);
                    _settingsService.SaveConfig();
                }
            }
        }
        catch (Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to add launcher search path", "LauncherSettingsViewModel.AddLauncherSearchPath");
        }
    }

    [RelayCommand]
    private void RemoveLauncherSearchPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Config.LauncherSearchPaths.Remove(path);
        _settingsService.SaveConfig();
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
