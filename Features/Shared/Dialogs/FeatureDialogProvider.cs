using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Settings.Sync;

namespace PromptMasterv6.Features.Shared.Dialogs;

public class FeatureDialogProvider : IFeatureDialogProvider
{
    public bool ShowOcrNotConfiguredDialog()
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowOcrNotConfiguredDialog());
        }

        var dialog = new ExternalTools.Dialogs.OcrNotConfiguredDialog();
        return dialog.ShowDialog() == true;
    }

    public string? ShowIconInputDialog(string? currentGeometry)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowIconInputDialog(currentGeometry));
        }

        var dialog = new IconInputDialog(currentGeometry);
        return dialog.ShowDialog() == true ? dialog.ResultGeometry : null;
    }

    public (bool Confirmed, string? ResultName) ShowNameInputDialog(string initialName)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowNameInputDialog(initialName));
        }

        var dialog = new NameInputDialog(initialName);
        if (dialog.ShowDialog() == true)
        {
            return (true, dialog.ResultName);
        }
        return (false, null);
    }

    public BackupFileItem? ShowBackupSelectionDialog(List<BackupFileItem> backups)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowBackupSelectionDialog(backups));
        }

        var dialog = new Settings.BackupSelectionDialog(backups);
        var activeWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
        dialog.Owner = activeWindow ?? System.Windows.Application.Current?.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            return dialog.SelectedBackup;
        }
        return null;
    }
}
