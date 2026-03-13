using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using PromptMasterv6.Features.Settings.Sync.OpenLogFolder;
using PromptMasterv6.Features.Settings.Sync.ClearLogs;
using System;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync;

public partial class SyncViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly DialogService _dialogService;
    private readonly IMediator _mediator;

    public AppConfig Config => _settingsService.Config;
    public LocalSettings LocalConfig => _settingsService.LocalConfig;

    [ObservableProperty] private bool isRestoreConfirmVisible;
    [ObservableProperty] private string? restoreStatus;
    [ObservableProperty] private RestoreStatusType currentRestoreStatus = RestoreStatusType.None;

    public SyncViewModel(
        SettingsService settingsService,
        DialogService dialogService,
        IMediator mediator)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _mediator = mediator;
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
        IsRestoreConfirmVisible = false;
        RestoreStatus = "正在从云端恢复数据...";
        CurrentRestoreStatus = RestoreStatusType.InProgress;

        var result = await _mediator.Send(new ManualRestoreFeature.Command());

        if (result.Success)
        {
            RestoreStatus = $"✅ {result.Message}";
            CurrentRestoreStatus = RestoreStatusType.Success;
        }
        else
        {
            RestoreStatus = $"❌ {result.Message}";
            CurrentRestoreStatus = RestoreStatusType.Failed;
        }
    }

    [RelayCommand]
    private async Task ManualLocalRestore()
    {
        var listResult = await _mediator.Send(new GetBackupListFeature.Query());

        if (!listResult.Success || listResult.Backups.Count == 0)
        {
            _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{listResult.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
            return;
        }

        var selected = _dialogService.ShowBackupSelectionDialog(listResult.Backups);
        if (selected == null) return;

        if (!_dialogService.ShowConfirmation($"确定要恢复到备份点：\n{selected.DisplayText} 吗？\n当前未保存的更改将会丢失。", "确认恢复"))
        {
            return;
        }

        RestoreStatus = "正在恢复本地数据...";
        CurrentRestoreStatus = RestoreStatusType.InProgress;

        var result = await _mediator.Send(new ManualLocalRestoreFeature.Command(selected.FilePath));

        if (result.Success)
        {
            RestoreStatus = $"✅ 本地恢复成功: {selected.FileName}";
            CurrentRestoreStatus = RestoreStatusType.Success;
        }
        else
        {
            RestoreStatus = $"❌ {result.Message}";
            CurrentRestoreStatus = RestoreStatusType.Failed;
        }
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        var result = await _mediator.Send(new ManualBackupFeature.Command());
        
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
        var selectResult = await _mediator.Send(
            new SelectExportPathFeature.Command($"PromptMasterv6_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip", "zip")
        );

        if (!selectResult.Success || selectResult.UserCancelled)
        {
            return;
        }

        var result = await _mediator.Send(new ExportConfigFeature.Command(selectResult.SelectedPath!));

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
        var selectResult = await _mediator.Send(
            new SelectImportPathFeature.Command("zip")
        );

        if (!selectResult.Success || selectResult.UserCancelled)
        {
            return;
        }

        if (_dialogService.ShowConfirmation("导入配置将覆盖当前的设置，确定要继续吗？\n(操作后将自动重启生效)", "确认导入"))
        {
            var result = await _mediator.Send(new ImportConfigFeature.Command(selectResult.SelectedPath!));

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
        var result = await _mediator.Send(new OpenLogFolderFeature.Command());

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
        var result = await _mediator.Send(new ClearLogsFeature.Command());

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
