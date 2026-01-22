using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PromptMasterv5.ViewModels;

namespace PromptMasterv5.Views
{
    public partial class QuickActionWindow : Window
    {
        private QuickActionViewModel? ViewModel => DataContext as QuickActionViewModel;

        public QuickActionWindow()
        {
            InitializeComponent();
            
            // Observe IsExpanded property changes
            this.Loaded += QuickActionWindow_Loaded;
        }

        private void QuickActionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                // Set actions for ViewModel to trigger
                ViewModel.OnExpandWindow = ExpandWindow;
                ViewModel.OnCloseWindow = () => this.Close();

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

        private void ExpandWindow()
        {
            // Show result and input areas
            ResultArea.Visibility = Visibility.Visible;
            InputArea.Visibility = Visibility.Visible;

            // Animate height expansion
            double targetHeight = SystemParameters.WorkArea.Height * 0.5; // 50% of screen height
            var animation = new DoubleAnimation
            {
                From = this.ActualHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.HeightProperty, animation);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Auto-close when focus is lost (after a small delay to avoid accidental closes)
            System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!this.IsActive && !ViewModel?.IsProcessing == true)
                    {
                        this.Close();
                    }
                });
            });
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Close on Esc
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
