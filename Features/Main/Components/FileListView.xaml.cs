using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.Components
{
    public partial class FileListView : System.Windows.Controls.UserControl
    {
        public FileListView()
        {
            InitializeComponent();
        }

        private void FileInlineEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem && promptItem.IsRenaming)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void FileInlineEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    promptItem.IsRenaming = false;
                    if (DataContext is FileManagerViewModel vm)
                    {
                        vm.RequestSaveCommand.Execute(null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (string.IsNullOrWhiteSpace(promptItem.Title))
                    {
                        promptItem.Title = "未命名提示词";
                    }
                    promptItem.IsRenaming = false;
                    e.Handled = true;
                }
            }
        }

        private void FileInlineEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                if (string.IsNullOrWhiteSpace(promptItem.Title))
                {
                    promptItem.Title = "未命名提示词";
                }
                promptItem.IsRenaming = false;
                if (DataContext is FileManagerViewModel vm)
                {
                    vm.RequestSaveCommand.Execute(null);
                }
            }
        }
    }
}
