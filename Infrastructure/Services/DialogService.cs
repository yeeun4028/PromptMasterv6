using Microsoft.Win32;
using PromptMasterv5.Core.Interfaces;
using System.Windows;
using System.Windows.Forms; // Alias for FolderBrowserDialog if using WinForms one, or use Ookii.Dialogs if available. Using WinForms for now as it's built-in.
// Note: FolderBrowserDialog is in System.Windows.Forms. Explicitly using full names to avoid conflict.

namespace PromptMasterv5.Infrastructure.Services
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
    }
}
