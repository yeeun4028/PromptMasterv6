using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Launcher.Messages;
using PromptMasterv6.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PromptMasterv6.Features.Main
{
    public partial class LaunchBarWindow : Window
    {
        private readonly MainViewModel _mainViewModel;
        private readonly LoggerService _logger;

        public LaunchBarWindow(MainViewModel mainViewModel, LoggerService logger)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            _logger = logger;
            this.DataContext = _mainViewModel;
            
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
            double taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;
            if (taskbarHeight <= 0)
            {
                taskbarHeight = SystemParameters.PrimaryScreenWidth - SystemParameters.WorkArea.Width;
            }
            if (taskbarHeight <= 0)
            {
                taskbarHeight = 48;
            }

            double topPos = SystemParameters.WorkArea.Top;
            if (topPos == 0)
            {
                topPos = taskbarHeight;
            }

            this.Top = topPos;
            this.Height = SystemParameters.WorkArea.Height - (topPos - SystemParameters.WorkArea.Top);
            this.Left = 0;
            UpdateWidth();
            
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
            double dpiX = 1.0;
            double dpiY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            int centerX = (int)((SystemParameters.PrimaryScreenWidth / 2) * dpiX);
            int centerY = (int)((SystemParameters.PrimaryScreenHeight / 2) * dpiY);
            NativeMethods.SetCursorPos(centerX, centerY);
        }

        private void ExecuteLaunchBarAction(LaunchBarItem item)
        {
            if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 500)
            {
                return;
            }
            _lastActionTime = DateTime.Now;

            try
            {
                if (item.ActionType == LaunchBarActionType.BuiltIn)
                {
                    switch (item.ActionTarget)
                    {
                        case "ToggleWindow":
                            WeakReferenceMessenger.Default.Send(new ToggleMainWindowMessage());
                            break;
                        case "ScreenshotTranslate":
                        case "Translate":
                            WeakReferenceMessenger.Default.Send(new TriggerTranslateMessage());
                            break;
                        case "OcrOnly":
                        case "Ocr":
                            WeakReferenceMessenger.Default.Send(new TriggerOcrMessage());
                            break;
                        case "Launcher":
                            WeakReferenceMessenger.Default.Send(new TriggerLauncherMessage());
                            break;
                        case "PinImage":
                            WeakReferenceMessenger.Default.Send(new TriggerPinToScreenMessage());
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

                MoveCursorToScreenCenter();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"Failed to execute launch bar action: {item.Label}", "LaunchBarWindow");
            }
        }

        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
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
