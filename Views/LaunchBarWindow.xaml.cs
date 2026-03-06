using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PromptMasterv6.Views
{
    public partial class LaunchBarWindow : Window
    {
        private readonly MainViewModel _mainViewModel;

        public LaunchBarWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            this.DataContext = _mainViewModel;
            
            
            // Re-evaluate visibility when config changes
            _mainViewModel.Config.PropertyChanged += Config_PropertyChanged;
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.EnableLaunchBar))
            {
                UpdateVisibility();
            }
            else if (e.PropertyName == nameof(AppConfig.LaunchBarWidth))
            {
                UpdateWidth();
            }
        }

        private void UpdateWidth()
        {
            Dispatcher.Invoke(() =>
            {
                this.Width = _mainViewModel.Config.LaunchBarWidth;
            });
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
            UpdateWidth(); // Ensure the width is set from code-behind (overrides/supplements XAML binding)
            
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (!_mainViewModel.Config.EnableLaunchBar || _mainViewModel.Config.LaunchBarItems.Count == 0)
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

        private DateTime _lastActionTime = DateTime.MinValue;

        private void MoveCursorToScreenCenter()
        {
            // Get DPI scale factor
            double dpiX = 1.0;
            double dpiY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Convert WPF logical coordinates to physical pixels
            int centerX = (int)((SystemParameters.PrimaryScreenWidth / 2) * dpiX);
            int centerY = (int)((SystemParameters.PrimaryScreenHeight / 2) * dpiY);
            NativeMethods.SetCursorPos(centerX, centerY);
        }

        private void ExecuteLaunchBarAction(LaunchBarItem item)
        {
            // Simple debounce/cooldown mechanism to prevent multiple triggers from mouse hovering over multiple items quickly
            if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 500)
            {
                return;
            }
            _lastActionTime = DateTime.Now;

            try
            {
                if (item.ActionType == LaunchBarActionType.BuiltIn)
                {
                    // Handle built-in commands
                    switch (item.ActionTarget)
                    {
                        case "ToggleWindow":
                            _mainViewModel.SimulateFullWindowHotkey();
                            break;
                        case "ScreenshotTranslate":
                        case "Translate": // config.json saves "Translate" for screenshot translation
                            _mainViewModel.ExternalToolsVM.TriggerTranslateCommand.Execute(null);
                            break;
                        case "OcrOnly":
                        case "Ocr": // Settings saves "Ocr" as ActionTarget for screenshot OCR
                            _mainViewModel.ExternalToolsVM.TriggerOcrCommand.Execute(null);
                            break;
                        case "Launcher":
                            _mainViewModel.HandleLauncherTriggered();
                            break;
                        case "PinImage":
                            _mainViewModel.ExternalToolsVM.TriggerPinToScreenCommand.Execute(null);
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

                // Move cursor to screen center after triggering the action
                MoveCursorToScreenCenter();
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
            IntPtr exStyle = Infrastructure.Services.NativeMethods.GetWindowLongPtr(hwnd, Infrastructure.Services.NativeMethods.GWL_EXSTYLE);
            Infrastructure.Services.NativeMethods.SetWindowLongPtr(hwnd, Infrastructure.Services.NativeMethods.GWL_EXSTYLE, (IntPtr)((long)exStyle | Infrastructure.Services.NativeMethods.WS_EX_NOACTIVATE));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_mainViewModel != null && _mainViewModel.Config != null)
            {
                 _mainViewModel.Config.PropertyChanged -= Config_PropertyChanged;
            }
            base.OnClosed(e);
        }
    }
}
