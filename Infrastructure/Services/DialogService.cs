using Microsoft.Win32;
using PromptMasterv6.Core.Interfaces;
using System.Windows;
using System.Windows.Forms;

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
            var dialogType = Type.GetType("PromptMasterv6.Features.ExternalTools.Dialogs.OcrNotConfiguredDialog, PromptMasterv6");
            if (dialogType == null)
            {
                ShowAlert("OCR 未配置，请在设置中配置 OCR 服务。", "提示");
                return false;
            }
            
            var dialog = System.Activator.CreateInstance(dialogType) as Window;
            return dialog?.ShowDialog() == true;
        }
    }
}
