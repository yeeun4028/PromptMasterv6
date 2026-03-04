using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv5.ViewModels;
using System.Text;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using Gma.System.MouseKeyHook;
using PromptMasterv5.Infrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Interop;
using System.Runtime.InteropServices;

using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using WpfControl = System.Windows.Controls.Control;
using WinFormsCursor = System.Windows.Forms.Cursor;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv5.ViewModels.Messages;

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        private DateTime _lastVarEnterTime = DateTime.MinValue;
        private DateTime _lastAddEnterTime = DateTime.MinValue;

        private DateTime _suppressAutoHideUntilUtc = DateTime.MinValue;

        public bool SuppressAutoActivation { get; set; }



        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;
        private bool _isBackupExiting = false;
        private DispatcherTimer? _hideTimer;
        private bool _isExternalCloseRequest = true; // 默认假设是外部关闭，SC_CLOSE 会标记为用户操作

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            LoggerService.Instance.LogInfo("[MainWindow] Constructor called", "MainWindow");

            // 设置窗口不在任务栏显示
            this.ShowInTaskbar = false;

            ViewModel = viewModel;
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // 【关键修复】：多重保障机制，绝对防御幽灵图标
            // 1. WPF 应用正常退出时触发
            Application.Current.Exit += Current_Exit;
            // 2. 进程被强制终止时触发（如任务管理器结束进程）
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            // 3. 控制台事件（Ctrl+C、关闭控制台窗口）
            SetConsoleCtrlHandler(ConsoleCtrlHandler, true);
        }

        private bool ConsoleCtrlHandler(CtrlTypes ctrlType)
        {
            LoggerService.Instance.LogInfo($"[ConsoleCtrlHandler] Received console event: {ctrlType}", "MainWindow");
            if (ctrlType == CtrlTypes.CTRL_CLOSE_EVENT || 
                ctrlType == CtrlTypes.CTRL_C_EVENT || 
                ctrlType == CtrlTypes.CTRL_BREAK_EVENT ||
                ctrlType == CtrlTypes.CTRL_LOGOFF_EVENT ||
                ctrlType == CtrlTypes.CTRL_SHUTDOWN_EVENT)
            {
                DisposeNotifyIcon();
            }
            return false; // 返回 false 让系统继续处理
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
            // 进程退出时的兜底清理
            DisposeNotifyIcon();
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            // 确保无论程序以何种方式结束，托盘图标必定被销毁
            DisposeNotifyIcon();
        }

        /// <summary>
        /// 安全释放托盘图标资源（可多次调用）
        /// </summary>
        private void DisposeNotifyIcon()
        {
            LoggerService.Instance.LogInfo($"[DisposeNotifyIcon] _notifyIcon={_notifyIcon != null}", "MainWindow");
            if (_notifyIcon != null)
            {
                try
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    LoggerService.Instance.LogInfo("[DisposeNotifyIcon] Disposed successfully", "MainWindow");
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogError($"[DisposeNotifyIcon] Error: {ex.Message}", "MainWindow");
                }
                _notifyIcon = null;
            }
        }

        /// <summary>
        /// 公共方法：强制清理托盘图标（可从 App 层调用）
        /// </summary>
        public void ForceCleanupNotifyIcon()
        {
            DisposeNotifyIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InitializeTrayIcon();
            RestoreWindowPosition();

            // 挂载窗口消息钩子，检测外部关闭请求（如 taskkill）
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
                LoggerService.Instance.LogInfo($"[OnSourceInitialized] WndProc hook attached, Handle={hwndSource.Handle}", "MainWindow");
            }
            else
            {
                LoggerService.Instance.LogError("[OnSourceInitialized] Failed to get HwndSource!", "MainWindow");
            }

            // 创建隐藏的消息窗口，用于接收 taskkill 发送的 WM_CLOSE
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
                    // 用户点击关闭按钮会先发送 WM_SYSCOMMAND/SC_CLOSE
                    // wParam 的低 4 位被系统保留，需要屏蔽
                    int cmd = wParam.ToInt32() & 0xFFF0;
                    if (cmd == SC_CLOSE)
                    {
                        // 标记为用户操作，不是外部关闭请求
                        _isExternalCloseRequest = false;
                        LoggerService.Instance.LogInfo("[WndProc] SC_CLOSE detected - User clicked close button", "MainWindow");
                    }
                    break;
                case WM_CLOSE:
                    // 如果没有先收到 SC_CLOSE，说明是外部关闭请求（taskkill）
                    // _isExternalCloseRequest 默认为 true，只有经过 SC_CLOSE 才会变成 false
                    LoggerService.Instance.LogInfo($"[WndProc] WM_CLOSE received - _isExternalCloseRequest={_isExternalCloseRequest}, Visibility={this.Visibility}", "MainWindow");
                    // 直接在这里处理外部关闭，不依赖 Closing 事件
                    if (_isExternalCloseRequest && !_isExiting)
                    {
                        LoggerService.Instance.LogInfo("[WndProc] External WM_CLOSE - forcing cleanup and shutdown", "MainWindow");
                        _isExiting = true;
                        Cleanup();
                        handled = true; // 标记已处理
                        // 发送关闭响应并退出
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Application.Current.Shutdown();
                        }));
                    }
                    break;
                case WM_QUERYENDSESSION:
                case WM_ENDSESSION:
                    // 系统关机/注销
                    _isExternalCloseRequest = true;
                    LoggerService.Instance.LogInfo("[WndProc] ENDSESSION - System shutdown", "MainWindow");
                    break;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 创建隐藏的消息窗口，用于接收 taskkill 发送的 WM_CLOSE
        /// </summary>
        private void CreateMessageWindow()
        {
            // 使用 WS_OVERLAPPEDWINDOW 样式，使窗口成为顶层窗口
            // taskkill 会向所有顶层窗口发送 WM_CLOSE
            var parameters = new HwndSourceParameters("PromptMasterMessageWindow")
            {
                WindowStyle = 0x00CF0000, // WS_OVERLAPPEDWINDOW (可关闭、有标题栏等)
                ExtendedWindowStyle = 0x00000080, // WS_EX_TOOLWINDOW (不在任务栏显示)
                PositionX = -32000, // 屏幕外
                PositionY = -32000,
                Width = 1,
                Height = 1,
                ParentWindow = IntPtr.Zero // 确保是顶层窗口
            };
            
            _messageHwndSource = new HwndSource(parameters);
            _messageHwndSource.AddHook(MessageWndProc);
            
            // 显示窗口（虽然位置在屏幕外）
            NativeMethods.ShowWindow(_messageHwndSource.Handle, NativeMethods.SW_SHOW);
            
            LoggerService.Instance.LogInfo($"[CreateMessageWindow] Message window created and shown, Handle={_messageHwndSource.Handle}", "MainWindow");
        }

        private IntPtr MessageWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLOSE || msg == WM_QUERYENDSESSION || msg == WM_ENDSESSION)
            {
                LoggerService.Instance.LogInfo($"[MessageWndProc] Received message 0x{msg:X4}, triggering cleanup", "MainWindow");
                if (!_isExiting)
                {
                    _isExiting = true;
                    _isExternalCloseRequest = true;
                    
                    // 清理托盘图标
                    DisposeNotifyIcon();
                    
                    // 退出应用
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Application.Current.Shutdown();
                    }));
                }
                handled = true;
                return new IntPtr(0); // 返回 TRUE 表示已处理
            }
            return IntPtr.Zero;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsSettingsOpen))
            {
                if (ViewModel.IsSettingsOpen)
                {
                    StopHideTimer();
                    this.Topmost = false;
                }
            }
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

                _notifyIcon.Text = "PromptMaster v5";
                _notifyIcon.Visible = true;

                // 使用 WPF ContextMenu 代替 WinForms 菜单
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
                // 如果窗口可见但不在前台（失去焦点），将其重新调回前台
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var foregroundHwnd = Infrastructure.Services.NativeMethods.GetForegroundWindow();
                if (hwnd != foregroundHwnd)
                {
                    StopHideTimer();
                    ViewModel.IsFullMode = true;
                    this.Show();
                    this.Activate();
                    this.Focus();
                    Infrastructure.Services.NativeMethods.SetForegroundWindow(hwnd);
                    return;
                }

                // 已在前台，执行隐藏
                this.Hide();
            }
            else
            {
                StopHideTimer();
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

                var windowManager = app.ServiceProvider.GetRequiredService<Core.Interfaces.IWindowManager>();
                await windowManager.ShowPinToScreenFromCaptureAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"贴图截图失败: {ex.Message}", "MainWindow");
            }
        }

        private void Tray_PinToScreen_Clipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                var windowManager = app.ServiceProvider.GetRequiredService<Core.Interfaces.IWindowManager>();
                if (!windowManager.ShowPinToScreenFromClipboard())
                {
                    HandyControl.Controls.Growl.Warning("剪贴板中没有图片");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"贴图剪贴板失败: {ex.Message}", "MainWindow");
            }
        }

        private void Tray_PinToScreen_CloseAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                var windowManager = app.ServiceProvider.GetRequiredService<Core.Interfaces.IWindowManager>();
                windowManager.CloseAllPinToScreenWindows();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"关闭贴图失败: {ex.Message}", "MainWindow");
            }
        }

        private async void Tray_Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;

            // 先执行异步保存 (等待它完成)
            if (ViewModel != null)
            {
                try
                {
                    LoggerService.Instance.LogInfo("执行退出前保存...", "MainWindow.Tray_Exit_Click");
                    // 在这里安全地异步等待
                    await ViewModel.PerformLocalBackup();
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogException(ex, "退出前保存失败", "MainWindow.Tray_Exit_Click");
                    System.Diagnostics.Debug.WriteLine($"退出前保存失败: {ex.Message}");
                }
            }

            // 清理所有资源
            Cleanup();

            // 释放 ViewModel 资源
            ViewModel?.Dispose();

            // 强制退出应用程序
            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            LoggerService.Instance.LogInfo($"[Closing] _isExiting={_isExiting}, _isBackupExiting={_isBackupExiting}, _isExternalCloseRequest={_isExternalCloseRequest}", "MainWindow");
            
            // 如果正在执行备份后退出，直接跳到清理阶段
            if (_isBackupExiting)
            {
                _isExiting = true;
                Cleanup();
                return;
            }

            // 如果是用户主动退出（从托盘菜单），允许关闭
            if (_isExiting)
            {
                Cleanup();
                return;
            }

            // 【关键修复】外部关闭请求（taskkill、系统关机等），允许关闭
            if (_isExternalCloseRequest)
            {
                LoggerService.Instance.LogInfo("[Closing] External close request - cleaning up and allowing close", "MainWindow");
                _isExiting = true;
                Cleanup();
                return;
            }

            // 检查是否有未保存的修改
            if (ViewModel.IsDirty)
            {
                // 检查是否配置了WebDAV同步
                bool hasWebDav = !string.IsNullOrWhiteSpace(ViewModel.Config.WebDavUrl) 
                              && !string.IsNullOrWhiteSpace(ViewModel.Config.Password);
                
                if (hasWebDav)
                {
                    // 配置了WebDAV，询问是否备份到云端
                    var dialog = new Views.ConfirmationDialog
                    {
                        Owner = this
                    };
                    dialog.ShowDialog();
                    var result = dialog.Result;
                    
                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true; // 取消关闭操作
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        e.Cancel = true; // 取消本次关闭，等待备份完成后再关闭
                        _ = BackupAndExitAsync();
                        return;
                    }
                    // 如果选 No，继续执行（App.OnExit 会自动保存到本地）
                }
                else
                {
                    // 未配置WebDAV，静默保存到本地（通过 App.OnExit）
                    // 不显示任何提示，因为本地保存是自动的
                }
            }

            // 如果不是通过退出菜单关闭，则隐藏窗口而不是关闭
            e.Cancel = true;  // 取消关闭事件
            this.Hide();       // 隐藏窗口
            _isExternalCloseRequest = true; // 重置标志，下次默认假设外部关闭
        }

        private void CleanupTimers()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer = null;
            }
        }

        private void Cleanup()
        {
            SaveWindowPosition();
            CleanupTimers();

            // 取消事件订阅
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // 清理托盘图标
            DisposeNotifyIcon();
        }

        private void SaveWindowPosition()
        {
            if (ViewModel?.Config == null) return;

            try
            {
                var config = ViewModel.Config;
                
                if (this.WindowState == WindowState.Maximized)
                {
                    config.MainWindowMaximized = true;
                }
                else
                {
                    config.MainWindowMaximized = false;

                    // 仅在值合法时更新，防止 Infinity/NaN 写入导致 ConfigService.Save 崩溃
                    if (double.IsFinite(this.Left))   config.MainWindowLeft   = this.Left;
                    if (double.IsFinite(this.Top))    config.MainWindowTop    = this.Top;
                    if (double.IsFinite(this.Width)  && this.Width  > 0) config.MainWindowWidth  = this.Width;
                    if (double.IsFinite(this.Height) && this.Height > 0) config.MainWindowHeight = this.Height;
                }
                
                LoggerService.Instance.LogInfo($"Window position saved: Left={config.MainWindowLeft}, Top={config.MainWindowTop}, Width={config.MainWindowWidth}, Height={config.MainWindowHeight}, Maximized={config.MainWindowMaximized}", "MainWindow.SaveWindowPosition");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save window position", "MainWindow.SaveWindowPosition");
            }
        }

        private void RestoreWindowPosition()
        {
            if (ViewModel?.Config == null) return;

            try
            {
                var config = ViewModel.Config;
                
                if (config.MainWindowMaximized)
                {
                    this.WindowState = WindowState.Maximized;
                    LoggerService.Instance.LogInfo("Window restored to maximized state", "MainWindow.RestoreWindowPosition");
                    return;
                }

                // IsFinite 同时排除 NaN 和 Infinity，比仅检查 !IsNaN 更严格
                if (double.IsFinite(config.MainWindowLeft) && double.IsFinite(config.MainWindowTop))
                {
                    this.Left = config.MainWindowLeft;
                    this.Top  = config.MainWindowTop;
                }

                if (double.IsFinite(config.MainWindowWidth)  && config.MainWindowWidth  > 0 &&
                    double.IsFinite(config.MainWindowHeight) && config.MainWindowHeight > 0)
                {
                    this.Width  = config.MainWindowWidth;
                    this.Height = config.MainWindowHeight;
                }

                LoggerService.Instance.LogInfo($"Window position restored: Left={this.Left}, Top={this.Top}, Width={this.Width}, Height={this.Height}", "MainWindow.RestoreWindowPosition");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to restore window position", "MainWindow.RestoreWindowPosition");
            }
        }


        /// <summary>
        /// 执行云端备份，完成后自动退出应用
        /// </summary>
        private async Task BackupAndExitAsync()
        {
            try
            {
                Title = "正在备份到云端... 请勿关闭";
                await ViewModel.BackupToCloudAsync();
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Exit backup failed", "MainWindow.BackupAndExitAsync");
            }
            finally
            {
                // 设置标志位，下次 Close() 时直接跳过对话框
                _isBackupExiting = true;
                _isExiting = true;
                this.Close();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (SuppressAutoActivation) return;

            StopHideTimer();
            this.Topmost = false; 
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (ViewModel.Config.AutoHide)
            {
                StartHideTimer();
            }
        }

        private void StopHideTimer()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer = null;
            }
        }

        private void StartHideTimer()
        {
            StopHideTimer();
            int delay = ViewModel?.Config?.AutoHideDelay ?? 10;
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            _hideTimer.Tick += (s, e) =>
            {
                StopHideTimer();
                if (!this.IsActive && this.Visibility == Visibility.Visible)
                {
                    this.Hide();
                }
            };
            _hideTimer.Start();
        }



        private async void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastVarEnterTime).TotalMilliseconds;

                if (isCtrlEnter)
                {
                    e.Handled = true;
                    // Execute Web Target Send
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                    return;
                }
                
                if (span < 500 && ViewModel.Config.EnableDoubleEnterSend)
                {
                    e.Handled = true;
                    // Execute Web Target Send
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                    _lastVarEnterTime = DateTime.MinValue;
                    return;
                }
                _lastVarEnterTime = now;
            }
        }

        private async void AdditionalInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastAddEnterTime).TotalMilliseconds;
                if (isCtrlEnter)
                {
                    e.Handled = true;
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                }
                else if (span < 500 && ViewModel.Config.EnableDoubleEnterSend)
                {
                    e.Handled = true;
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                    _lastAddEnterTime = DateTime.MinValue;
                }
                else
                {
                    _lastAddEnterTime = now;
                    int caretIndex = textBox.CaretIndex;
                    int lineIndex = textBox.GetLineIndexFromCharacterIndex(caretIndex);
                    if (lineIndex < 0) return;
                    string lineText = textBox.GetLineText(lineIndex);
                    var match = Regex.Match(lineText, @"^(\s*)(\d+)\.(\s+)");
                    if (match.Success)
                    {
                        string indentation = match.Groups[1].Value;
                        int currentNumber = int.Parse(match.Groups[2].Value);
                        string spacing = match.Groups[3].Value;
                        string insertText = $"\n{indentation}{currentNumber + 1}.{spacing}";
                        textBox.SelectedText = insertText;
                        textBox.CaretIndex += insertText.Length;
                        e.Handled = true;
                    }
                }
            }
        }

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null && pb.Password != ViewModel.Config.Password) pb.Password = ViewModel.Config.Password; }
        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null) ViewModel.Config.Password = pb.Password; }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
        }
        private void Block3ContentEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckAndExitEditMode();
        }

        private void Block3TitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckAndExitEditMode();
        }

        private void CheckAndExitEditMode()
        {
            if (ViewModel == null || !ViewModel.IsEditMode) return;

            // 检查新的焦点是否仍在编辑区内
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (IsBlock3Editor(focused)) return;

            ViewModel.IsEditMode = false;
            ViewModel.RequestSaveCommand.Execute(null);
        }

        private bool IsBlock3Editor(DependencyObject? obj)
        {
            if (obj == null) return false;
            if (obj == Block3TitleEditor) return true;
            if (obj == Block3ContentEditor) return true;
            
            // 检查是否是编辑器的子元素（例如右键菜单或内部结构）
            if (Block3TitleEditor != null && Block3TitleEditor.IsAncestorOf(obj)) return true;
            if (Block3ContentEditor != null && Block3ContentEditor.IsAncestorOf(obj)) return true;
            
            return false;
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
