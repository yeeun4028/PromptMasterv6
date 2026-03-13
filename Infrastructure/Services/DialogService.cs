using Microsoft.Win32;
using System.Windows;
using System.Windows.Forms;
using PromptMasterv6.Features.Shared.Dialogs;

namespace PromptMasterv6.Infrastructure.Services
{
    public class DialogService
    {
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
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowOcrNotConfiguredDialog());
            }

            var dialogType = Type.GetType("PromptMasterv6.Features.ExternalTools.Dialogs.OcrNotConfiguredDialog, PromptMasterv6");
            if (dialogType == null)
            {
                ShowAlert("OCR 未配置，请在设置中配置 OCR 服务。", "提示");
                return false;
            }
            
            var dialog = System.Activator.CreateInstance(dialogType) as Window;
            return dialog?.ShowDialog() == true;
        }

        public string? ShowIconInputDialog(string? currentGeometry = "")
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

        public BackupFileItem? ShowBackupSelectionDialog(System.Collections.Generic.List<BackupFileItem> backups)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => ShowBackupSelectionDialog(backups));
            }

            var dialog = new Features.Settings.BackupSelectionDialog(backups);
            var activeWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
            dialog.Owner = activeWindow ?? System.Windows.Application.Current?.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedBackup;
            }
            return null;
        }
    }
}
