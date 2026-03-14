using MediatR;
using Microsoft.Xaml.Behaviors;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.CompleteFileRename;
using PromptMasterv6.Features.Workspace.CancelFileRename;

namespace PromptMasterv6.Features.Workspace.RenameFile
{
    public class InlineRenameBehavior : Behavior<System.Windows.Controls.TextBox>
    {
        public static readonly System.Windows.DependencyProperty MediatorProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Mediator),
                typeof(IMediator),
                typeof(InlineRenameBehavior),
                new System.Windows.PropertyMetadata(null));

        public IMediator Mediator
        {
            get => (IMediator)GetValue(MediatorProperty);
            set => SetValue(MediatorProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.PreviewKeyDown += OnKeyDown;
            AssociatedObject.LostFocus += OnLostFocus;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.PreviewKeyDown -= OnKeyDown;
            AssociatedObject.LostFocus -= OnLostFocus;
            base.OnDetaching();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AssociatedObject.DataContext is PromptItem promptItem && promptItem.IsRenaming)
            {
                AssociatedObject.Focus();
                AssociatedObject.SelectAll();
            }
        }

        private async void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (AssociatedObject.DataContext is PromptItem promptItem)
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    if (Mediator != null)
                    {
                        await Mediator.Send(new CompleteFileRenameFeature.Command(promptItem, ShouldSave: true));
                    }
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (Mediator != null)
                    {
                        await Mediator.Send(new CancelFileRenameFeature.Command(promptItem));
                    }
                    e.Handled = true;
                }
            }
        }

        private async void OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AssociatedObject.DataContext is PromptItem promptItem)
            {
                if (Mediator != null)
                {
                    await Mediator.Send(new CompleteFileRenameFeature.Command(promptItem, ShouldSave: true));
                }
            }
        }
    }
}
