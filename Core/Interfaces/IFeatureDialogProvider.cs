using System.Collections.Generic;
using PromptMasterv6.Features.Settings.Sync;

namespace PromptMasterv6.Core.Interfaces;

public interface IFeatureDialogProvider
{
    bool ShowOcrNotConfiguredDialog();
    string? ShowIconInputDialog(string? currentGeometry);
    (bool Confirmed, string? ResultName) ShowNameInputDialog(string initialName);
    BackupFileItem? ShowBackupSelectionDialog(List<BackupFileItem> backups);
}
