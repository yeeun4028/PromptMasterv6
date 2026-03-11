using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Behaviors;

public class EscapeToHideWindowBehavior : Behavior<Window>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        base.OnDetaching();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AssociatedObject.Hide();
            e.Handled = true;
        }
    }
}
