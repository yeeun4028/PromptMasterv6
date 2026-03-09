using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PromptMasterv6.Features.Launcher
{
    public partial class LauncherWindow : Window
    {
        private readonly DispatcherTimer _tabHoverTimer;
        private System.Windows.Point _dragStartPoint;
        private bool _isDragging;
        private System.Windows.Controls.RadioButton? _hoveredTab;

        public LauncherWindow()
        {
            InitializeComponent();
            Closing += (s, e) => _isClosing = true;

            _tabHoverTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _tabHoverTimer.Tick += TabHoverTimer_Tick;
            PreviewMouseWheel += Window_PreviewMouseWheel;
        }

        public LauncherWindow(LauncherViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            double browserTopUiHeight = 85; 
            
            Width = workArea.Width;
            Height = workArea.Height - browserTopUiHeight;
            Left = workArea.Left;
            Top = workArea.Top + browserTopUiHeight;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is LauncherViewModel vm && sender is System.Windows.Controls.RadioButton rb)
            {
                vm.SelectCategory(rb.Tag?.ToString() ?? "Bookmark");
            }
        }

        private void Tab_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton tab)
            {
                _hoveredTab = tab;
                _tabHoverTimer.Start();
            }
        }

        private void Tab_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _tabHoverTimer.Stop();
            _hoveredTab = null;
        }

        private void TabHoverTimer_Tick(object? sender, EventArgs e)
        {
            _tabHoverTimer.Stop();
            if (_hoveredTab != null)
            {
                _hoveredTab.IsChecked = true;
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is LauncherViewModel vm && vm.Config.IsLauncherSinglePageDisplayEnabled)
            {
                return;
            }

            if (e.Delta > 0)
            {
                if (TabTool.IsChecked == true) TabApplication.IsChecked = true;
                else if (TabApplication.IsChecked == true) TabBookmark.IsChecked = true;
                else if (TabBookmark.IsChecked == true) TabTool.IsChecked = true;
            }
            else
            {
                if (TabBookmark.IsChecked == true) TabApplication.IsChecked = true;
                else if (TabApplication.IsChecked == true) TabTool.IsChecked = true;
                else if (TabTool.IsChecked == true) TabBookmark.IsChecked = true;
            }
        }

        private void ItemButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void ItemButton_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                System.Windows.Point currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    if (sender is System.Windows.Controls.Button button && button.DataContext is LauncherItem item)
                    {
                        System.Windows.DragDrop.DoDragDrop(button, new System.Windows.DataObject("LauncherItem", item), System.Windows.DragDropEffects.Move);
                    }
                }
            }
        }

        private void ItemButton_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
            {
                var sourceItem = e.Data.GetData("LauncherItem") as LauncherItem;
                if (sender is System.Windows.Controls.Button targetButton && targetButton.DataContext is LauncherItem targetItem)
                {
                    if (DataContext is LauncherViewModel vm && sourceItem != null && sourceItem != targetItem)
                    {
                        vm.MoveItem(sourceItem, targetItem);
                    }
                }
            }
        }

        private void ItemButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
            }
            else if (sender is System.Windows.Controls.Button button)
            {
                if (button.Command?.CanExecute(button.CommandParameter) == true)
                {
                    e.Handled = true;
                    button.Command.Execute(button.CommandParameter);
                    SafeClose();
                }
            }
            _isDragging = false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            SafeClose();
        }

        private bool _isClosing = false;
        private void SafeClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            try { Close(); } catch { }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SafeClose();
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            SafeClose();
        }
    }
}
