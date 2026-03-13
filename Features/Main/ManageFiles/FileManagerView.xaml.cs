using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.FileManager
{
    public partial class FileManagerView : System.Windows.Controls.UserControl
    {
        private FileManagerViewModel? ViewModel => DataContext as FileManagerViewModel;

        public FileManagerView() : this(App.Services.GetRequiredService<FileManagerViewModel>())
        {
        }

        public FileManagerView(FileManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
                if (e.Key == Key.Enter)
                {
                    promptItem.IsRenaming = false;
                    ViewModel?.RequestSaveCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
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
                ViewModel?.RequestSaveCommand.Execute(null);
            }
        }
    }
}
