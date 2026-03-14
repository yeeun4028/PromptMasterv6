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
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace;

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
        private readonly TrayService _trayService;
        private readonly IWorkspaceState _workspaceState;

        private DateTime _suppressAutoHideUntilUtc = DateTime.MinValue;

        public bool SuppressAutoActivation { get; set; }

        private bool _isExiting = false;
        private bool _isBackupExiting = false;
        private bool _isExternalCloseRequest = true;

        public MainWindow(
            MainViewModel viewModel, 
            LoggerService logger, 
            TrayService trayService,
            IWorkspaceState workspaceState,
            WorkspaceContainerView workspaceContainerView)
        {
            InitializeComponent();

            _logger = logger;
            _trayService = trayService;
            _workspaceState = workspaceState;
            _logger.LogInfo("[MainWindow] Constructor called", "MainWindow");

            this.ShowInTaskbar = false;

            ViewModel = viewModel;
            this.DataContext = ViewModel;

            WorkspaceViewControl.Content = workspaceContainerView;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            WeakReferenceMessenger.Default.Register<ToggleMainWindowMessage>(this, (_, __) =>
            {
                ToggleWindowVisibility();
            });

            Application.Current.Exit += Current_Exit;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            SetConsoleCtrlHandler(ConsoleCtrlHandler, true);

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Initialize();
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
                _trayService.Dispose();
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
            _trayService.Dispose();
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            _trayService.Dispose();
        }

        public void ForceCleanupNotifyIcon()
        {
            _trayService.Dispose();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            _trayService.Initialize(
                ToggleWindowVisibility,
                DoHandleExitRequest,
                () => _workspaceState.IsDirty);

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
                    
                    _trayService.Dispose();
                    
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

        public void ToggleWindowVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                _suppressAutoHideUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
                ViewModel.IsFullMode = true;
                this.Show();
                this.Activate();
                this.Focus();
                Infrastructure.Services.NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
        }

        private async Task DoHandleExitRequest()
        {
            _isExiting = true;

            if (ViewModel != null)
            {
                try
                {
                    _logger.LogInfo("执行退出前保存...", "MainWindow.HandleExitRequest");
                    await ViewModel.PerformLocalBackup();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "退出前保存失败", "MainWindow.HandleExitRequest");
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

            if (_workspaceState.IsDirty)
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

            WeakReferenceMessenger.Default.Unregister<ToggleMainWindowMessage>(this);

            _trayService.Dispose();
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
            if (DateTime.UtcNow < _suppressAutoHideUntilUtc) return;

            if (ViewModel.Config.AutoHide)
            {
                var isSettingsOpen = Application.Current.Windows.OfType<Window>().Any(w => w.GetType().Name == "SettingsWindow" && w.IsVisible);
                if (!isSettingsOpen)
                {
                    this.Hide();
                }
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
