using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Behaviors
{
    public class WindowDragBehavior : Behavior<FrameworkElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            base.OnDetaching();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                var window = Window.GetWindow(AssociatedObject);
                window?.DragMove();
            }
        }
    }
}
