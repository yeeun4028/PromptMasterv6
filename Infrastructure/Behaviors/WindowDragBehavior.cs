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
            var window = Window.GetWindow(AssociatedObject);
            if (window == null) return;

            // 双击切换最大化/还原
            if (e.ClickCount == 2)
            {
                if (window.WindowState == WindowState.Maximized)
                {
                    window.WindowState = WindowState.Normal;
                }
                else
                {
                    window.WindowState = WindowState.Maximized;
                }
                e.Handled = true;
                return;
            }

            // 单击执行拖拽
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        }
    }
}
