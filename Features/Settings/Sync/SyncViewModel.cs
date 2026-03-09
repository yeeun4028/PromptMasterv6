using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync;

public partial class SyncViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDataService _dataService;
    private readonly FileDataService _localDataService;
    private readonly IDialogService _dialogService;
    private readonly IWindowManager _windowManager;

    private MainViewModel? _mainViewModel;

    public AppConfig Config => _settingsService.Config;
    public LocalSettings LocalConfig => _settingsService.LocalConfig;

    [ObservableProperty] private bool isRestoreConfirmVisible;
    [ObservableProperty] private string? restoreStatus;
    [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public SyncViewModel(
        ISettingsService settingsService,
        IDataService dataService,
        FileDataService localDataService,
        IDialogService dialogService,
        IWindowManager windowManager)
    {
        _settingsService = settingsService;
        _dataService = dataService;
        _localDataService = localDataService;
        _dialogService = dialogService;
        _windowManager = windowManager;
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
        if (_mainViewModel == null)
        {
            _dialogService.ShowAlert("系统初始化未完成", "错误");
            return;
        }

        _settingsService.SaveConfig();

        IsRestoreConfirmVisible = false;
        RestoreStatus = "正在从云端恢复数据...";
        RestoreStatusColor = System.Windows.Media.Brushes.Orange;

        try
        {
            var data = await _dataService.LoadAsync();

            if (data == null || ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0))
            {
                RestoreStatus = "❌ 云端没有数据可恢复 (或连接失败)";
                RestoreStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            ApplyRestoredData(data);

            RestoreStatus = $"✅ 成功恢复 {data.Folders?.Count ?? 0} 个文件夹和 {data.Files?.Count ?? 0} 个提示词";
            RestoreStatusColor = System.Windows.Media.Brushes.Green;

            LoggerService.Instance.LogInfo($"Restored {data.Folders?.Count ?? 0} folders and {data.Files?.Count ?? 0} files", "SyncViewModel.ManualRestore");
        }
        catch (Exception ex)
        {
            RestoreStatus = $"❌ 恢复失败: {ex.Message}";
            RestoreStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to restore from cloud", "SyncViewModel.ManualRestore");
        }
    }

    private void ApplyRestoredData(AppData data)
    {
        if (_mainViewModel == null) return;

        _mainViewModel.Files.Clear();
        _mainViewModel.Folders.Clear();

        if (data.Folders != null && data.Folders.Count > 0)
        {
            foreach (var folder in data.Folders)
            {
                _mainViewModel.Folders.Add(folder);
            }
            _mainViewModel.SelectedFolder = _mainViewModel.Folders.FirstOrDefault();
        }
        else
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            _mainViewModel.Folders.Add(defaultFolder);
            _mainViewModel.SelectedFolder = defaultFolder;
        }

        if (data.Files != null && data.Files.Count > 0)
        {
            foreach (var file in data.Files)
            {
                if (string.IsNullOrWhiteSpace(file.FolderId) && _mainViewModel.SelectedFolder != null)
                {
                    file.FolderId = _mainViewModel.SelectedFolder.Id;
                }
                _mainViewModel.Files.Add(file);
            }
        }

        _mainViewModel.UpdateFilesViewFilter();
        _mainViewModel.FilesView?.Refresh();
        WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
    }

    [RelayCommand]
    private async Task ManualLocalRestore()
    {
        if (_mainViewModel == null)
        {
            _dialogService.ShowAlert("系统初始化未完成", "错误");
            return;
        }

        var service = _localDataService as FileDataService;
        if (service == null) return;

        var backups = service.GetBackups();
        LoggerService.Instance.LogInfo($"Found {backups.Count} backups in {service.BackupDirectory}", "SyncViewModel.ManualLocalRestore");

        if (backups.Count == 0)
        {
            _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{service.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
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

        try
        {
            var data = await service.RestoreLocalBackupAsync(selected.FilePath);
            if (data == null)
            {
                throw new Exception("读取备份文件失败");
            }

            ApplyRestoredData(data);

            RestoreStatus = $"✅ 本地恢复成功: {selected.FileName}";
            RestoreStatusColor = System.Windows.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            RestoreStatus = $"❌ 恢复失败: {ex.Message}";
            RestoreStatusColor = System.Windows.Media.Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        if (_mainViewModel == null)
        {
            _dialogService.ShowAlert("系统初始化未完成", "错误");
            return;
        }

        try
        {
            await _dataService.SaveAsync(_mainViewModel.Folders, _mainViewModel.Files);
            _mainViewModel.LocalConfig.LastCloudSyncTime = DateTime.Now;
            _mainViewModel.IsDirty = false;
            _mainViewModel.IsEditMode = false;
            _settingsService.SaveLocalConfig();
            LoggerService.Instance.LogInfo("Manual cloud backup successful", "SyncViewModel.ManualBackup");
            _dialogService.ShowToast("云端备份成功！", "Success");
        }
        catch (Exception ex)
        {
            _dialogService.ShowAlert($"备份失败: {ex.Message}", "错误");
            LoggerService.Instance.LogException(ex, "Failed to manual backup", "SyncViewModel.ManualBackup");
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
            try
            {
                _settingsService.ExportSettings(dialog.FileName);
                _dialogService.ShowToast("配置导出成功！", "Success");
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"配置导出失败: {ex.Message}", "错误");
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
                try
                {
                    _settingsService.ImportSettings(dialog.FileName);
                    
                    WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
                    
                    OnPropertyChanged(nameof(Config));
                    OnPropertyChanged(nameof(LocalConfig));

                    _dialogService.ShowToast("配置导入成功！", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowAlert($"配置导入失败: {ex.Message}", "错误");
                }
            }
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var logPath = LoggerService.Instance.GetLogDirectory();
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
            LoggerService.Instance.LogException(ex, "Failed to open log folder", "SyncViewModel.OpenLogFolder");
            _dialogService.ShowAlert($"无法打开日志文件夹: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        try
        {
            LoggerService.Instance.ClearLogs();
            _dialogService.ShowToast("日志已清除", "Success");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to clear logs", "SyncViewModel.ClearLogs");
            _dialogService.ShowAlert($"清除日志失败: {ex.Message}", "错误");
        }
    }
}
