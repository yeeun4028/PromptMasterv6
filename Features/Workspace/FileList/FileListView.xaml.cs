using PromptMasterv6.Features.Shared.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PromptMasterv6.Features.Workspace.FileList;

public partial class FileListView : System.Windows.Controls.UserControl
{
    public FileListView()
    {
        InitializeComponent();
    }

    private void FileInlineEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem item)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void FileInlineEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem item)
        {
            if (e.Key == Key.Enter)
            {
                item.IsRenaming = false;
                Keyboard.ClearFocus();
            }
            else if (e.Key == Key.Escape)
            {
                item.IsRenaming = false;
                Keyboard.ClearFocus();
            }
        }
    }

    private void FileInlineEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem item)
        {
            item.IsRenaming = false;
        }
    }
}
