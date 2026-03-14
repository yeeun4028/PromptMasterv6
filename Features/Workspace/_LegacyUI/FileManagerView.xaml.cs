using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.CompleteFileRename;
using PromptMasterv6.Features.Workspace.CancelFileRename;

namespace PromptMasterv6.Features.Workspace._LegacyUI
{
    public partial class FileManagerView : System.Windows.Controls.UserControl
    {
        private readonly IMediator _mediator;

        public FileManagerView() : this(
            App.Services.GetRequiredService<FileManagerViewModel>(),
            App.Services.GetRequiredService<IMediator>())
        {
        }

        public FileManagerView(FileManagerViewModel viewModel, IMediator mediator)
        {
            InitializeComponent();
            DataContext = viewModel;
            _mediator = mediator;
        }

        private void FileInlineEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem && promptItem.IsRenaming)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private async void FileInlineEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                if (e.Key == Key.Enter)
                {
                    await _mediator.Send(new CompleteFileRenameFeature.Command(promptItem, ShouldSave: true));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    await _mediator.Send(new CancelFileRenameFeature.Command(promptItem));
                    e.Handled = true;
                }
            }
        }

        private async void FileInlineEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                await _mediator.Send(new CompleteFileRenameFeature.Command(promptItem, ShouldSave: true));
            }
        }
    }
}
