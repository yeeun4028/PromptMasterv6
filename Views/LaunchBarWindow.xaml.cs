using PromptMasterv5.Core.Models;
using PromptMasterv5.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PromptMasterv5.Views
{
    public partial class LaunchBarWindow : Window
    {
        private readonly MainViewModel _mainViewModel;
        private DispatcherTimer _fullScreenCheckTimer;
        private bool _isHiddenDueToFullScreen = false;

        public LaunchBarWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            this.DataContext = _mainViewModel;

            // Timer to check for full-screen apps
            _fullScreenCheckTimer = new DispatcherTimer();
            _fullScreenCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
            _fullScreenCheckTimer.Tick += FullScreenCheckTimer_Tick;
            _fullScreenCheckTimer.Start();
            
            // Re-evaluate visibility when config changes
            _mainViewModel.Config.PropertyChanged += Config_PropertyChanged;
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.EnableLaunchBar))
            {
                UpdateVisibility();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Calculate taskbar height (or breadth) to use as top margin
            double taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;
            if (taskbarHeight <= 0)
            {
                taskbarHeight = SystemParameters.PrimaryScreenWidth - SystemParameters.WorkArea.Width;
            }
            if (taskbarHeight <= 0)
            {
                taskbarHeight = 48; // Sensible default
            }

            double topPos = SystemParameters.WorkArea.Top;
            if (topPos == 0)
            {
                // If taskbar is at bottom or sides, leave a gap at the top equivalent to the taskbar height
                topPos = taskbarHeight;
            }

            // Lock to primary screen left edge, adjust top and height
            this.Top = topPos;
            this.Height = SystemParameters.WorkArea.Height - (topPos - SystemParameters.WorkArea.Top);
            this.Left = 0;
            
            UpdateVisibility();
        }

        private void FullScreenCheckTimer_Tick(object? sender, EventArgs e)
        {
            bool isFullScreenActive = Infrastructure.Services.NativeMethods.IsForegroundFullScreen();
            
            if (isFullScreenActive && !_isHiddenDueToFullScreen)
            {
                _isHiddenDueToFullScreen = true;
                UpdateVisibility();
            }
            else if (!isFullScreenActive && _isHiddenDueToFullScreen)
            {
                _isHiddenDueToFullScreen = false;
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            if (!_mainViewModel.Config.EnableLaunchBar || _isHiddenDueToFullScreen || _mainViewModel.Config.LaunchBarItems.Count == 0)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                // Ensure it stays on top without stealing focus constantly
                this.Topmost = true;
            }
        }

        private void Segment_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is LaunchBarItem item)
            {
                ExecuteLaunchBarAction(item);
            }
        }

        private void ExecuteLaunchBarAction(LaunchBarItem item)
        {
            try
            {
                if (item.ActionType == LaunchBarActionType.BuiltIn)
                {
                    // Handle built-in commands
                    switch (item.ActionTarget)
                    {
                        case "ToggleWindow":
                            _mainViewModel.OnWindowHotkeyPressed();
                            break;
                        case "ScreenshotTranslate":
                        case "Translate": // config.json saves "Translate" for screenshot translation
                            _mainViewModel.ExternalToolsVM.TriggerTranslateCommand.Execute(null);
                            break;
                        case "OcrOnly":
                            _mainViewModel.ExternalToolsVM.TriggerOcrCommand.Execute(null);
                            break;
                        case "Launcher":
                            _mainViewModel.HandleLauncherTriggered();
                            break;
                    }
                }
                else if (item.ActionType == LaunchBarActionType.CustomApp)
                {
                    if (!string.IsNullOrWhiteSpace(item.ActionTarget))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.ActionTarget,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, $"Failed to execute launch bar action: {item.Label}", "LaunchBarWindow");
            }
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Set WS_EX_NOACTIVATE to ensure this window NEVER takes focus, 
            // even when this.Show() is called or it is clicked, preventing it from
            // stealing focus from other popups (like TranslationPopup)
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int exStyle = Infrastructure.Services.NativeMethods.GetWindowLong(hwnd, Infrastructure.Services.NativeMethods.GWL_EXSTYLE);
            Infrastructure.Services.NativeMethods.SetWindowLong(hwnd, Infrastructure.Services.NativeMethods.GWL_EXSTYLE, exStyle | Infrastructure.Services.NativeMethods.WS_EX_NOACTIVATE);
        }

        protected override void OnClosed(EventArgs e)
        {
            _fullScreenCheckTimer.Stop();
            if (_mainViewModel != null && _mainViewModel.Config != null)
            {
                 _mainViewModel.Config.PropertyChanged -= Config_PropertyChanged;
            }
            base.OnClosed(e);
        }
    }
}
