using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using System;

namespace PromptMasterv6.Features.Settings.Launcher;

public partial class LauncherSettingsViewModel : ObservableObject
{
    private readonly AddSearchPathFeature.Handler _addSearchPathHandler;
    private readonly RemoveSearchPathFeature.Handler _removeSearchPathHandler;
    private readonly SettingsService _settingsService;
    private readonly LoggerService _logger;

    public AppConfig Config => _settingsService.Config;

    public LauncherSettingsViewModel(
        AddSearchPathFeature.Handler addSearchPathHandler,
        RemoveSearchPathFeature.Handler removeSearchPathHandler,
        SettingsService settingsService,
        LoggerService logger)
    {
        _addSearchPathHandler = addSearchPathHandler;
        _removeSearchPathHandler = removeSearchPathHandler;
        _settingsService = settingsService;
        _logger = logger;
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
                var result = _addSearchPathHandler.Handle(new AddSearchPathFeature.Command(dialog.SelectedPath));
                if (!result.Success && result.ErrorMessage != null)
                {
                    _logger.LogInfo($"Add search path skipped: {result.ErrorMessage}", "LauncherSettingsViewModel.AddLauncherSearchPath");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to add launcher search path", "LauncherSettingsViewModel.AddLauncherSearchPath");
        }
    }

    [RelayCommand]
    private void RemoveLauncherSearchPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _removeSearchPathHandler.Handle(new RemoveSearchPathFeature.Command(path));
    }
}
