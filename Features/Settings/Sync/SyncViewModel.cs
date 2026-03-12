using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using PromptMasterv6.Features.Settings.Sync.OpenLogFolder;
using PromptMasterv6.Features.Settings.Sync.ClearLogs;
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
    private readonly SelectExportPathFeature.Handler _selectExportPathHandler;
    private readonly SelectImportPathFeature.Handler _selectImportPathHandler;
    private readonly OpenLogFolderFeature.Handler _openLogFolderHandler;
    private readonly ClearLogsFeature.Handler _clearLogsHandler;

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
        ImportConfigFeature.Handler importConfigHandler,
        SelectExportPathFeature.Handler selectExportPathHandler,
        SelectImportPathFeature.Handler selectImportPathHandler,
        OpenLogFolderFeature.Handler openLogFolderHandler,
        ClearLogsFeature.Handler clearLogsHandler)
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
        _selectExportPathHandler = selectExportPathHandler;
        _selectImportPathHandler = selectImportPathHandler;
        _openLogFolderHandler = openLogFolderHandler;
        _clearLogsHandler = clearLogsHandler;
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
    private async Task ExportConfig()
    {
        // 1. 选择导出路径
        var selectResult = await _selectExportPathHandler.Handle(
            new SelectExportPathFeature.Command($"PromptMasterv6_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip", "zip"),
            CancellationToken.None
        );

        if (!selectResult.Success || selectResult.UserCancelled)
        {
            return;  // 用户取消或失败
        }

        // 2. 执行导出
        var result = _exportConfigHandler.Handle(new ExportConfigFeature.Command(selectResult.SelectedPath!));

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
    private async Task ImportConfig()
    {
        // 1. 选择导入路径
        var selectResult = await _selectImportPathHandler.Handle(
            new SelectImportPathFeature.Command("zip"),
            CancellationToken.None
        );

        if (!selectResult.Success || selectResult.UserCancelled)
        {
            return;  // 用户取消或失败
        }

        // 2. 确认导入
        if (_dialogService.ShowConfirmation("导入配置将覆盖当前的设置，确定要继续吗？\n(操作后将自动重启生效)", "确认导入"))
        {
            var result = _importConfigHandler.Handle(new ImportConfigFeature.Command(selectResult.SelectedPath!));

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

    [RelayCommand]
    private async Task OpenLogFolder()
    {
        var result = await _openLogFolderHandler.Handle(new OpenLogFolderFeature.Command());

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
    private async Task ClearLogs()
    {
        var result = await _clearLogsHandler.Handle(new ClearLogsFeature.Command());

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
