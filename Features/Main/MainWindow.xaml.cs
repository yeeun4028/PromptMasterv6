using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Dialogs;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Infrastructure.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq;
using Gma.System.MouseKeyHook;
using PromptMasterv6.Infrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Interop;
using System.Runtime.InteropServices;

using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PromptMasterv6.Features.Main
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private readonly LoggerService _logger;

        private DateTime _suppressAutoHideUntilUtc = DateTime.MinValue;

        public bool SuppressAutoActivation { get; set; }



        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;
        private bool _isBackupExiting = false;

        private bool _isExternalCloseRequest = true;

        private DispatcherTimer? _routingAnimTimer;
        private bool _routingAnimState = false;
        private System.Drawing.Icon? _defaultIcon;
        private System.Drawing.Icon? _processingIcon;

        public MainWindow(MainViewModel viewModel, LoggerService logger)
        {
            InitializeComponent();

            _logger = logger;
            _logger.LogInfo("[MainWindow] Constructor called", "MainWindow");

            this.ShowInTaskbar = false;

            ViewModel = viewModel;
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            WeakReferenceMessenger.Default.Register<ToggleMainWindowMessage>(this, (_, __) =>
            {
                ToggleWindowVisibility();
            });

            Application.Current.Exit += Current_Exit;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            SetConsoleCtrlHandler(ConsoleCtrlHandler, true);
        }

        private bool ConsoleCtrlHandler(CtrlTypes ctrlType)
        {
            _logger.LogInfo($"[ConsoleCtrlHandler] Received console event: {ctrlType}", "MainWindow");
            if (ctrlType == CtrlTypes.CTRL_CLOSE_EVENT || 
                ctrlType == CtrlTypes.CTRL_C_EVENT || 
                ctrlType == CtrlTypes.CTRL_BREAK_EVENT ||
                ctrlType == CtrlTypes.CTRL_LOGOFF_EVENT ||
                ctrlType == CtrlTypes.CTRL_SHUTDOWN_EVENT)
            {
                DisposeNotifyIcon();
            }
            return false;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate handler, bool add);

        private delegate bool ConsoleCtrlHandlerDelegate(CtrlTypes ctrlType);

        private enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 2,
            CTRL_CLOSE_EVENT = 5,
            CTRL_LOGOFF_EVENT = 6,
            CTRL_SHUTDOWN_EVENT = 7
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            DisposeNotifyIcon();
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            DisposeNotifyIcon();
        }

        private void DisposeNotifyIcon()
        {
            _logger.LogInfo($"[DisposeNotifyIcon] _notifyIcon={_notifyIcon != null}", "MainWindow");
            if (_notifyIcon != null)
            {
                try
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _logger.LogInfo("[DisposeNotifyIcon] Disposed successfully", "MainWindow");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[DisposeNotifyIcon] Error: {ex.Message}", "MainWindow");
                }
                _notifyIcon = null;
            }
        }


        public void ForceCleanupNotifyIcon()
        {
            DisposeNotifyIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InitializeTrayIcon();
            RestoreWindowPosition();

            this.SizeChanged += (_, __) => SaveWindowPosition();

            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
                _logger.LogInfo($"[OnSourceInitialized] WndProc hook attached, Handle={hwndSource.Handle}", "MainWindow");
            }
            else
            {
                _logger.LogError("[OnSourceInitialized] Failed to get HwndSource!", "MainWindow");
            }

            CreateMessageWindow();
        }

        private const int WM_CLOSE = 0x0010;
        private const int WM_ENDSESSION = 0x0016;
        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_DESTROY = 0x0002;

        private HwndSource? _messageHwndSource;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SYSCOMMAND:
                    int cmd = wParam.ToInt32() & 0xFFF0;
                    if (cmd == SC_CLOSE)
                    {
                        _isExternalCloseRequest = false;
                        _logger.LogInfo("[WndProc] SC_CLOSE detected - User clicked close button", "MainWindow");
                    }
                    break;
                case WM_CLOSE:
                    _logger.LogInfo($"[WndProc] WM_CLOSE received - _isExternalCloseRequest={_isExternalCloseRequest}, Visibility={this.Visibility}", "MainWindow");
                    if (_isExternalCloseRequest && !_isExiting)
                    {
                        _logger.LogInfo("[WndProc] External WM_CLOSE - forcing cleanup and shutdown", "MainWindow");
                        _isExiting = true;
                        Cleanup();
                        handled = true;
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Application.Current.Shutdown();
                        }));
                    }
                    break;
                case WM_QUERYENDSESSION:
                case WM_ENDSESSION:
                    _isExternalCloseRequest = true;
                    _logger.LogInfo("[WndProc] ENDSESSION - System shutdown", "MainWindow");
                    break;
            }
            return IntPtr.Zero;
        }

        private void CreateMessageWindow()
        {
            var parameters = new HwndSourceParameters("PromptMasterv6MessageWindow")
            {
                WindowStyle = 0x00CF0000,
                ExtendedWindowStyle = 0x00000080,
                PositionX = -32000,
                PositionY = -32000,
                Width = 1,
                Height = 1,
                ParentWindow = IntPtr.Zero
            };
            
            _messageHwndSource = new HwndSource(parameters);
            _messageHwndSource.AddHook(MessageWndProc);
            
            NativeMethods.ShowWindow(_messageHwndSource.Handle, NativeMethods.SW_SHOW);
            
            _logger.LogInfo($"[CreateMessageWindow] Message window created and shown, Handle={_messageHwndSource.Handle}", "MainWindow");
        }

        private IntPtr MessageWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLOSE || msg == WM_QUERYENDSESSION || msg == WM_ENDSESSION)
            {
                _logger.LogInfo($"[MessageWndProc] Received message 0x{msg:X4}, triggering cleanup", "MainWindow");
                if (!_isExiting)
                {
                    _isExiting = true;
                    _isExternalCloseRequest = true;
                    
                    DisposeNotifyIcon();
                    
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Application.Current.Shutdown();
                    }));
                }
                handled = true;
                return new IntPtr(0);
            }
            return IntPtr.Zero;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }



        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                
                try
                {
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                }
                catch
                {
                    try
                    {
                        if (System.IO.File.Exists("pro_icon.ico"))
                            _notifyIcon.Icon = new System.Drawing.Icon("pro_icon.ico");
                        else
                            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                    catch
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }

                _notifyIcon.Text = "PromptMaster v6";
                _notifyIcon.Visible = true;

                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    {
                        var menu = this.Resources["TrayMenu"] as System.Windows.Controls.ContextMenu;
                        if (menu != null)
                        {
                            menu.DataContext = this.DataContext;
                            TrayMenuHelper.ShowContextMenu(menu);
                        }
                    }
                    else if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        ToggleWindowVisibility();
                    }
                };
                
                _defaultIcon = _notifyIcon.Icon;
                _processingIcon = System.Drawing.SystemIcons.Information;
                
                _routingAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _routingAnimTimer.Tick += (s, e) =>
                {
                    if (_notifyIcon == null) return;
                    _routingAnimState = !_routingAnimState;
                    _notifyIcon.Icon = _routingAnimState ? _processingIcon : _defaultIcon;
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"托盘图标初始化失败: {ex.Message}");
            }
        }

        public void ToggleWindowVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                ViewModel.IsFullMode = true;
                this.Show();
                this.Activate();
                this.Focus();
                Infrastructure.Services.NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
        }

        private void Tray_ToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowVisibility();
        }

        private async void Tray_PinToScreen_Capture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                var windowManager = app.ServiceProvider.GetRequiredService<WindowManager>();
                await windowManager.ShowPinToScreenFromCaptureAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"贴图截图失败: {ex.Message}", "MainWindow");
            }
        }

        private void Tray_PinToScreen_Clipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                var windowManager = app.ServiceProvider.GetRequiredService<WindowManager>();
                if (!windowManager.ShowPinToScreenFromClipboard())
                {
                    HandyControl.Controls.Growl.Warning("剪贴板中没有图片");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"贴图剪贴板失败: {ex.Message}", "MainWindow");
            }
        }

        private void Tray_PinToScreen_CloseAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                var windowManager = app.ServiceProvider.GetRequiredService<WindowManager>();
                windowManager.CloseAllPinToScreenWindows();
            }
            catch (Exception ex)
            {
                _logger.LogError($"关闭贴图失败: {ex.Message}", "MainWindow");
            }
        }

        private async void Tray_Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;

            if (ViewModel != null)
            {
                try
                {
                    _logger.LogInfo("执行退出前保存...", "MainWindow.Tray_Exit_Click");
                    await ViewModel.PerformLocalBackup();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "退出前保存失败", "MainWindow.Tray_Exit_Click");
                    System.Diagnostics.Debug.WriteLine($"退出前保存失败: {ex.Message}");
                }
            }

            Cleanup();

            ViewModel?.Dispose();

            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _logger.LogInfo($"[Closing] _isExiting={_isExiting}, _isBackupExiting={_isBackupExiting}, _isExternalCloseRequest={_isExternalCloseRequest}", "MainWindow");
            
            if (_isBackupExiting)
            {
                _isExiting = true;
                Cleanup();
                return;
            }

            if (_isExiting)
            {
                Cleanup();
                return;
            }

            if (_isExternalCloseRequest)
            {
                _logger.LogInfo("[Closing] External close request - cleaning up and allowing close", "MainWindow");
                _isExiting = true;
                Cleanup();
                return;
            }

            if (ViewModel.FileManagerVM.IsDirty)
            {
                bool hasWebDav = !string.IsNullOrWhiteSpace(ViewModel.Config.WebDavUrl) 
                              && !string.IsNullOrWhiteSpace(ViewModel.Config.Password);
                
                if (hasWebDav)
                {
                    var dialog = new ConfirmationDialog
                    {
                        Owner = this
                    };
                    dialog.ShowDialog();
                    var result = dialog.Result;
                    
                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        _ = BackupAndExitAsync();
                        return;
                    }
                }
            }

            e.Cancel = true;
            this.Hide();
            _isExternalCloseRequest = true;
        }

        private void Cleanup()
        {
            SaveWindowPosition();

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            DisposeNotifyIcon();
        }

        private void SaveWindowPosition()
        {
            if (ViewModel?.Config == null) return;

            try
            {
                var config = ViewModel.Config;
                
                if (double.IsFinite(this.Width)  && this.Width  > 0) config.MainWindowWidth  = Math.Round(this.Width, 1);
                if (double.IsFinite(this.Height) && this.Height > 0) config.MainWindowHeight = Math.Round(this.Height, 1);
                
                _logger.LogInfo($"Window size saved: Width={config.MainWindowWidth}, Height={config.MainWindowHeight}", "MainWindow.SaveWindowPosition");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to save window size", "MainWindow.SaveWindowPosition");
            }
        }

        private void RestoreWindowPosition()
        {
            if (ViewModel?.Config == null) return;

            try
            {
                var config = ViewModel.Config;

                if (double.IsFinite(config.MainWindowWidth)  && config.MainWindowWidth  > 0 &&
                    double.IsFinite(config.MainWindowHeight) && config.MainWindowHeight > 0)
                {
                    this.Width  = config.MainWindowWidth;
                    this.Height = config.MainWindowHeight;
                }

                _logger.LogInfo($"Window size restored: Width={this.Width}, Height={this.Height}", "MainWindow.RestoreWindowPosition");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to restore window size", "MainWindow.RestoreWindowPosition");
            }
        }


        private async Task BackupAndExitAsync()
        {
            try
            {
                Title = "正在备份到云端... 请勿关闭";
                WeakReferenceMessenger.Default.Send(new RequestBackupMessage());
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Exit backup failed", "MainWindow.BackupAndExitAsync");
            }
            finally
            {
                _isBackupExiting = true;
                _isExiting = true;
                this.Close();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (SuppressAutoActivation) return;

            this.Topmost = false; 
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (ViewModel.Config.AutoHide)
            {
                var isSettingsOpen = Application.Current.Windows.OfType<Window>().Any(w => w.GetType().Name == "SettingsWindow" && w.IsVisible);
                if (!isSettingsOpen)
                {
                    this.Hide();
                }
            }
        }



        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null && pb.Password != ViewModel.Config.Password) pb.Password = ViewModel.Config.Password; }
        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null) ViewModel.Config.Password = pb.Password; }

        private void FileInlineEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is PromptItem promptItem && promptItem.IsRenaming)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void FileInlineEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                if (e.Key == Key.Enter)
                {
                    promptItem.IsRenaming = false;
                    ViewModel?.FileManagerVM.RequestSaveCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (string.IsNullOrWhiteSpace(promptItem.Title))
                    {
                        promptItem.Title = "未命名提示词";
                    }
                    promptItem.IsRenaming = false;
                    e.Handled = true;
                }
            }
        }

        private void FileInlineEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is PromptItem promptItem)
            {
                if (string.IsNullOrWhiteSpace(promptItem.Title))
                {
                    promptItem.Title = "未命名提示词";
                }
                promptItem.IsRenaming = false;
                ViewModel?.FileManagerVM.RequestSaveCommand.Execute(null);
            }
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
