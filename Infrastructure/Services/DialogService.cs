using Microsoft.Win32;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.ExternalTools.Dialogs;
using System.Windows;
using System.Windows.Forms; // Alias for FolderBrowserDialog if using WinForms one, or use Ookii.Dialogs if available. Using WinForms for now as it's built-in.
// Note: FolderBrowserDialog is in System.Windows.Forms. Explicitly using full names to avoid conflict.

namespace PromptMasterv6.Infrastructure.Services
{
    public class DialogService : IDialogService
    {
        public void ShowAlert(string message, string title)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowConfirmation(string message, string title)
        {
            var result = System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void ShowToast(string message, string type = "Info")
        {
            // 使用非 Global 的 Growl 调用，避免在 MainWindow 未激活时（如文件对话框打开期间）
            // HandyControl 创建临时空白 Window
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
            var dialog = new OcrNotConfiguredDialog();
            return dialog.ShowDialog() == true;
        }
    }
}
