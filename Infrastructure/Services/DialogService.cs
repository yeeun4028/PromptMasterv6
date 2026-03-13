using Microsoft.Win32;
using System.Windows;
using System.Windows.Forms;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class DialogService
    {
        private readonly IFeatureDialogProvider _featureDialogProvider;

        public DialogService(IFeatureDialogProvider featureDialogProvider)
        {
            _featureDialogProvider = featureDialogProvider;
        }

        public void ShowAlert(string message, string title)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => ShowAlert(message, title));
                return;
            }
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowConfirmation(string message, string title)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowConfirmation(message, title));
            }
            var result = System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void ShowToast(string message, string type = "Info")
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                switch (type.ToLower())
                {
                    case "success":
                        HandyControl.Controls.Growl.Success(message);
                        break;
                    case "warning":
                        HandyControl.Controls.Growl.Warning(message);
                        break;
                    case "error":
                        HandyControl.Controls.Growl.Error(message);
                        break;
                    case "info":
                    default:
                        HandyControl.Controls.Growl.Info(message);
                        break;
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        public string? ShowOpenFileDialog(string filter)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowOpenFileDialog(filter));
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter
            };
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        public string[]? ShowOpenFilesDialog(string filter)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowOpenFilesDialog(filter));
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileNames;
            }
            return null;
        }

        public string? ShowSaveFileDialog(string filter, string defaultName)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowSaveFileDialog(filter, defaultName));
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                FileName = defaultName
            };
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        public string? ShowFolderBrowserDialog(string description = "")
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowFolderBrowserDialog(description));
            }

            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.UseDescriptionForTitle = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        public bool ShowOcrNotConfiguredDialog()
        {
            return _featureDialogProvider.ShowOcrNotConfiguredDialog();
        }

        public string? ShowIconInputDialog(string? currentGeometry = "")
        {
            return _featureDialogProvider.ShowIconInputDialog(currentGeometry);
        }

        public (bool Confirmed, string? ResultName) ShowNameInputDialog(string initialName)
        {
            return _featureDialogProvider.ShowNameInputDialog(initialName);
        }

        public Features.Settings.Sync.BackupFileItem? ShowBackupSelectionDialog(System.Collections.Generic.List<Features.Settings.Sync.BackupFileItem> backups)
        {
            return _featureDialogProvider.ShowBackupSelectionDialog(backups);
        }
    }
}
