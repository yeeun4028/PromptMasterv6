using System.Collections.Generic;
using System.Windows;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Views
{
    public partial class BackupSelectionDialog : Window
    {
        public BackupFileItem? SelectedBackup { get; private set; }

        public BackupSelectionDialog(List<BackupFileItem> backups)
        {
            InitializeComponent();
            BackupList.ItemsSource = backups;
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (BackupList.SelectedItem is BackupFileItem item)
            {
                SelectedBackup = item;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("请先选择一个备份文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
