using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync;

public partial class SyncViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly FileDataService _localDataService;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;
    private readonly ManualRestoreFeature.Handler _manualRestoreHandler;
    private readonly ManualLocalRestoreFeature.Handler _manualLocalRestoreHandler;
    private readonly ManualBackupFeature.Handler _manualBackupHandler;
    private readonly ExportConfigFeature.Handler _exportConfigHandler;
    private readonly ImportConfigFeature.Handler _importConfigHandler;

    public AppConfig Config => _settingsService.Config;
    public LocalSettings LocalConfig => _settingsService.LocalConfig;

    [ObservableProperty] private bool isRestoreConfirmVisible;
    [ObservableProperty] private string? restoreStatus;
    [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

    public SyncViewModel(
        SettingsService settingsService,
        FileDataService localDataService,
        DialogService dialogService,
        LoggerService logger,
        ManualRestoreFeature.Handler manualRestoreHandler,
        ManualLocalRestoreFeature.Handler manualLocalRestoreHandler,
        ManualBackupFeature.Handler manualBackupHandler,
        ExportConfigFeature.Handler exportConfigHandler,
        ImportConfigFeature.Handler importConfigHandler)
    {
        _settingsService = settingsService;
        _localDataService = localDataService;
        _dialogService = dialogService;
        _logger = logger;
        _manualRestoreHandler = manualRestoreHandler;
        _manualLocalRestoreHandler = manualLocalRestoreHandler;
        _manualBackupHandler = manualBackupHandler;
        _exportConfigHandler = exportConfigHandler;
        _importConfigHandler = importConfigHandler;
    }

    [RelayCommand]
    private void ShowRestoreConfirm()
    {
        IsRestoreConfirmVisible = true;
        RestoreStatus = null;
    }

    [RelayCommand]
    private void CancelRestoreConfirm()
    {
        IsRestoreConfirmVisible = false;
    }

    [RelayCommand]
    private async Task ManualRestore()
    {
        _settingsService.SaveConfig();

        IsRestoreConfirmVisible = false;
        RestoreStatus = "正在从云端恢复数据...";
        RestoreStatusColor = System.Windows.Media.Brushes.Orange;

        var result = await _manualRestoreHandler.Handle(new ManualRestoreFeature.Command());

        if (result.Success)
        {
            RestoreStatus = $"✅ {result.Message}";
            RestoreStatusColor = System.Windows.Media.Brushes.Green;
        }
        else
        {
            RestoreStatus = $"❌ {result.Message}";
            RestoreStatusColor = System.Windows.Media.Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task ManualLocalRestore()
    {
        var backups = _localDataService.GetBackups();
        _logger.LogInfo($"Found {backups.Count} backups in {_localDataService.BackupDirectory}", "SyncViewModel.ManualLocalRestore");

        if (backups.Count == 0)
        {
            _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{_localDataService.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
            return;
        }

        var dialog = new BackupSelectionDialog(backups);
        
        var activeWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
        dialog.Owner = activeWindow ?? System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true || dialog.SelectedBackup == null) return;

        var selected = dialog.SelectedBackup;
        if (!_dialogService.ShowConfirmation($"确定要恢复到备份点：\n{selected.DisplayText} 吗？\n当前未保存的更改将会丢失。", "确认恢复"))
        {
            return;
        }

        RestoreStatus = "正在恢复本地数据...";
        RestoreStatusColor = System.Windows.Media.Brushes.Orange;

        var result = await _manualLocalRestoreHandler.Handle(new ManualLocalRestoreFeature.Command(selected.FilePath));

        if (result.Success)
        {
            RestoreStatus = $"✅ 本地恢复成功: {selected.FileName}";
            RestoreStatusColor = System.Windows.Media.Brushes.Green;
        }
        else
        {
            RestoreStatus = $"❌ {result.Message}";
            RestoreStatusColor = System.Windows.Media.Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        var result = await _manualBackupHandler.Handle(new ManualBackupFeature.Command());
        
        if (result.Success)
        {
            _dialogService.ShowToast(result.Message, "Success");
        }
        else
        {
            _dialogService.ShowAlert(result.Message, "错误");
        }
    }

    [RelayCommand]
    private void ExportConfig()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出配置",
            Filter = "配置文件压缩包 (*.zip)|*.zip",
            FileName = $"PromptMasterv6_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip"
        };

        var owner = System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);

        if (dialog.ShowDialog(owner) == true)
        {
            var result = _exportConfigHandler.Handle(new ExportConfigFeature.Command(dialog.FileName));
            
            if (result.Success)
            {
                _dialogService.ShowToast(result.Message, "Success");
            }
            else
            {
                _dialogService.ShowAlert(result.Message, "错误");
            }
        }
    }

    [RelayCommand]
    private void ImportConfig()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入配置",
            Filter = "配置文件压缩包 (*.zip)|*.zip"
        };

        var owner = System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);

        if (dialog.ShowDialog(owner) == true)
        {
            if (_dialogService.ShowConfirmation("导入配置将覆盖当前的设置，确定要继续吗？\n(操作后将自动重启生效)", "确认导入"))
            {
                var result = _importConfigHandler.Handle(new ImportConfigFeature.Command(dialog.FileName));
                
                if (result.Success)
                {
                    OnPropertyChanged(nameof(Config));
                    OnPropertyChanged(nameof(LocalConfig));
                    _dialogService.ShowToast(result.Message, "Success");
                }
                else
                {
                    _dialogService.ShowAlert(result.Message, "错误");
                }
            }
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var logPath = _logger.GetLogDirectory();
            if (System.IO.Directory.Exists(logPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", logPath);
            }
            else
            {
                _dialogService.ShowToast("日志文件夹不存在", "Info");
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to open log folder", "SyncViewModel.OpenLogFolder");
            _dialogService.ShowAlert($"无法打开日志文件夹: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        try
        {
            _logger.ClearLogs();
            _dialogService.ShowToast("日志已清除", "Success");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to clear logs", "SyncViewModel.ClearLogs");
            _dialogService.ShowAlert($"清除日志失败: {ex.Message}", "错误");
        }
    }
}
