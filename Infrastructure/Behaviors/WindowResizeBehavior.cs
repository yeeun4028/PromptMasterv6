using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace PromptMasterv6.Infrastructure.Behaviors
{
    public class WindowResizeBehavior : Behavior<Thumb>
    {
        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(nameof(Direction), typeof(string), typeof(WindowResizeBehavior), new PropertyMetadata(""));

        public string Direction
        {
            get { return (string)GetValue(DirectionProperty); }
            set { SetValue(DirectionProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.DragDelta += OnDragDelta;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.DragDelta -= OnDragDelta;
            base.OnDetaching();
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            var window = Window.GetWindow(AssociatedObject);
            if (window == null) return;
            if (window.WindowState != WindowState.Normal) return;

            const double minWidth = 260;
            const double minHeight = 90;

            double newLeft = window.Left;
            double newTop = window.Top;
            double newWidth = window.Width;
            double newHeight = window.Height;

            var tag = Direction;
            
            bool resizeLeft = tag.Contains("Left", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Top", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Bottom", StringComparison.OrdinalIgnoreCase);
            bool resizeRight = tag.Contains("Right", StringComparison.OrdinalIgnoreCase);
            bool resizeTop = tag.Contains("Top", StringComparison.OrdinalIgnoreCase);
            bool resizeBottom = tag.Contains("Bottom", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Top", StringComparison.OrdinalIgnoreCase);

            if (tag == "Left") { resizeLeft = true; resizeTop = false; resizeBottom = false; resizeRight = false; }
            if (tag == "Right") { resizeRight = true; resizeTop = false; resizeBottom = false; resizeLeft = false; }
            if (tag == "Top") { resizeTop = true; resizeLeft = false; resizeRight = false; resizeBottom = false; }
            if (tag == "Bottom") { resizeBottom = true; resizeLeft = false; resizeRight = false; resizeTop = false; }

            if (resizeLeft)
            {
                double proposedWidth = newWidth - e.HorizontalChange;
                if (proposedWidth >= minWidth)
                {
                    newWidth = proposedWidth;
                    newLeft += e.HorizontalChange;
                }
                else
                {
                    newLeft += newWidth - minWidth;
                    newWidth = minWidth;
                }
            }
            else if (resizeRight)
            {
                newWidth = Math.Max(minWidth, newWidth + e.HorizontalChange);
            }

            if (resizeTop)
            {
                double proposedHeight = newHeight - e.VerticalChange;
                if (proposedHeight >= minHeight)
                {
                    newHeight = proposedHeight;
                    newTop += e.VerticalChange;
                }
                else
                {
                    newTop += newHeight - minHeight;
                    newHeight = minHeight;
                }
            }
            else if (resizeBottom)
            {
                newHeight = Math.Max(minHeight, newHeight + e.VerticalChange);
            }

            window.Left = newLeft;
            window.Top = newTop;
            window.Width = newWidth;
            window.Height = newHeight;
        }
    }
}
