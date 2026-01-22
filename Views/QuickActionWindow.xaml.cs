using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using PromptMasterv5.ViewModels;

namespace PromptMasterv5.Views
{
    public partial class QuickActionWindow : Window
    {
        private QuickActionViewModel? ViewModel => DataContext as QuickActionViewModel;
        private const double CompactHeight = 60;
        private const double ExpandedHeight = 400;
        private const double CompactWidth = 600;

        public QuickActionWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position window at right edge, vertically centered
            PositionWindow();

            if (ViewModel != null)
            {
                // Set actions for ViewModel to trigger
                ViewModel.OnExpandWindow = ExpandWindow;
                ViewModel.OnCloseWindow = () => this.Close();
                ViewModel.OnToggleLargeMode = ToggleLargeMode;

                // Monitor expansion state
                ViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(ViewModel.IsExpanded))
                    {
                        if (ViewModel.IsExpanded)
                        {
                            ExpandWindow();
                        }
                    }
                };
            }
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;

            // Right edge with 20px margin
            this.Left = workArea.Right - this.Width - 20;

            // Vertical center
            this.Top = workArea.Top + (workArea.Height - this.Height) / 2;
        }

        private void ExpandWindow()
        {
            // Show message and input areas
            MessageArea.Visibility = Visibility.Visible;
            InputArea.Visibility = Visibility.Visible;

            // Animate height expansion
            double targetHeight = ExpandedHeight;
            var animation = new DoubleAnimation
            {
                From = this.ActualHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.HeightProperty, animation);

            // Reposition to keep toolbar visually centered
            var workArea = SystemParameters.WorkArea;
            double newTop = workArea.Top + (workArea.Height - targetHeight) / 2;
            
            var topAnimation = new DoubleAnimation
            {
                From = this.Top,
                To = newTop,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.TopProperty, topAnimation);

            // Set focus to input box after expansion
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InputTextBox?.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ToggleLargeMode()
        {
            var workArea = SystemParameters.WorkArea;

            double targetWidth;
            double targetHeight;

            if (ViewModel?.IsLargeMode == true)
            {
                // Large mode: 2/3 of screen
                targetWidth = workArea.Width * 2 / 3;
                targetHeight = workArea.Height * 2 / 3;
            }
            else
            {
                // Compact mode
                targetWidth = CompactWidth;
                targetHeight = ViewModel?.IsExpanded == true ? ExpandedHeight : CompactHeight;
            }

            // Animate width
            var widthAnimation = new DoubleAnimation
            {
                From = this.ActualWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Animate height
            var heightAnimation = new DoubleAnimation
            {
                From = this.ActualHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.WidthProperty, widthAnimation);
            this.BeginAnimation(Window.HeightProperty, heightAnimation);

            // Reposition to keep right-edge centered
            double newLeft = workArea.Right - targetWidth - 20;
            double newTop = workArea.Top + (workArea.Height - targetHeight) / 2;

            var leftAnimation = new DoubleAnimation
            {
                From = this.Left,
                To = newLeft,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var topAnimation = new DoubleAnimation
            {
                From = this.Top,
                To = newTop,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.LeftProperty, leftAnimation);
            this.BeginAnimation(Window.TopProperty, topAnimation);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Don't auto-close if window is locked or processing
            if (ViewModel?.IsWindowLocked == true || ViewModel?.IsProcessing == true)
                return;

            // Auto-close when focus is lost (after a small delay to avoid accidental closes)
            System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!this.IsActive && ViewModel?.IsProcessing != true && ViewModel?.IsWindowLocked != true)
                    {
                        this.Close();
                    }
                });
            });
        }

        private void InputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Enter sends message, Shift+Enter creates new line
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Shift+Enter - insert newline (default behavior)
                    return;
                }
                else
                {
                    // Enter - send message
                    e.Handled = true;
                    ViewModel?.SendMessageCommand.Execute(null);
                }
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Close on Esc only if not locked
            if (e.Key == Key.Escape && ViewModel?.IsWindowLocked != true)
            {
                this.Close();
            }
        }
    }
}
