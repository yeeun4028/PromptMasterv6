using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Views
{
    public partial class BackupSelectionDialog : Window
    {
        public BackupFileItem? SelectedBackup { get; private set; }

        public BackupSelectionDialog(List<BackupFileItem> backups)
        {
            InitializeComponent();
            BackupList.ItemsSource = backups;

            // Show empty state if no backups available
            if (backups == null || backups.Count == 0)
            {
                BackupList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Enable the Restore button only when an item is selected
        /// </summary>
        private void BackupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RestoreButton.IsEnabled = BackupList.SelectedItem != null;
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (BackupList.SelectedItem is BackupFileItem item)
            {
                SelectedBackup = item;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
