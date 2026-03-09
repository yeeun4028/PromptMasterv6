using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Dialogs;
using CommunityToolkit.Mvvm.Messaging;
using System.Text;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using Gma.System.MouseKeyHook;
using PromptMasterv6.Infrastructure.Helpers;
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

namespace PromptMasterv6.Features.Main
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
        private bool _isExternalCloseRequest = true;

        private DispatcherTimer? _routingAnimTimer;
        private bool _routingAnimState = false;
        private System.Drawing.Icon? _defaultIcon;
        private System.Drawing.Icon? _processingIcon;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            LoggerService.Instance.LogInfo("[MainWindow] Constructor called", "MainWindow");

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
            LoggerService.Instance.LogInfo($"[ConsoleCtrlHandler] Received console event: {ctrlType}", "MainWindow");
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
                LoggerService.Instance.LogInfo($"[OnSourceInitialized] WndProc hook attached, Handle={hwndSource.Handle}", "MainWindow");
            }
            else
            {
                LoggerService.Instance.LogError("[OnSourceInitialized] Failed to get HwndSource!", "MainWindow");
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
                        LoggerService.Instance.LogInfo("[WndProc] SC_CLOSE detected - User clicked close button", "MainWindow");
                    }
                    break;
                case WM_CLOSE:
                    LoggerService.Instance.LogInfo($"[WndProc] WM_CLOSE received - _isExternalCloseRequest={_isExternalCloseRequest}, Visibility={this.Visibility}", "MainWindow");
                    if (_isExternalCloseRequest && !_isExiting)
                    {
                        LoggerService.Instance.LogInfo("[WndProc] External WM_CLOSE - forcing cleanup and shutdown", "MainWindow");
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
                    LoggerService.Instance.LogInfo("[WndProc] ENDSESSION - System shutdown", "MainWindow");
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

            if (ViewModel != null)
            {
                try
                {
                    LoggerService.Instance.LogInfo("执行退出前保存...", "MainWindow.Tray_Exit_Click");
                    await ViewModel.PerformLocalBackup();
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogException(ex, "退出前保存失败", "MainWindow.Tray_Exit_Click");
                    System.Diagnostics.Debug.WriteLine($"退出前保存失败: {ex.Message}");
                }
            }

            Cleanup();

            ViewModel?.Dispose();

            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            LoggerService.Instance.LogInfo($"[Closing] _isExiting={_isExiting}, _isBackupExiting={_isBackupExiting}, _isExternalCloseRequest={_isExternalCloseRequest}", "MainWindow");
            
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
                LoggerService.Instance.LogInfo("[Closing] External close request - cleaning up and allowing close", "MainWindow");
                _isExiting = true;
                Cleanup();
                return;
            }

            if (ViewModel.IsDirty)
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
                
                LoggerService.Instance.LogInfo($"Window size saved: Width={config.MainWindowWidth}, Height={config.MainWindowHeight}", "MainWindow.SaveWindowPosition");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save window size", "MainWindow.SaveWindowPosition");
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

                LoggerService.Instance.LogInfo($"Window size restored: Width={this.Width}, Height={this.Height}", "MainWindow.RestoreWindowPosition");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to restore window size", "MainWindow.RestoreWindowPosition");
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
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Exit backup failed", "MainWindow.BackupAndExitAsync");
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
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                    return;
                }
                
                if (span < 500 && ViewModel.Config.EnableDoubleEnterSend)
                {
                    e.Handled = true;
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

        private void CheckAndExitEditMode()
        {
            if (ViewModel == null || !ViewModel.IsEditMode) return;

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (IsBlock3Editor(focused)) return;

            ViewModel.IsEditMode = false;
            ViewModel.RequestSaveCommand.Execute(null);
        }

        private bool IsBlock3Editor(DependencyObject? obj)
        {
            if (obj == null) return false;
            if (obj == Block3ContentEditor) return true;
            
            if (Block3ContentEditor != null && Block3ContentEditor.IsAncestorOf(obj)) return true;
            
            return false;
        }

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
                    ViewModel?.RequestSaveCommand.Execute(null);
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
                ViewModel?.RequestSaveCommand.Execute(null);
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


        private void MarkdownViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DependencyObject root) return;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                var fdsv = FindDescendant<FlowDocumentScrollViewer>(root);
                if (fdsv != null)
                {
                    fdsv.ContextMenu = BuildEditorContextMenu();
                }
            });
        }

        private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var nested = FindDescendant<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        internal static ContextMenu BuildEditorContextMenu()
        {
            var menu = new ContextMenu
            {
                Background = Application.Current.Resources["CardBackground"] as System.Windows.Media.Brush,
                BorderBrush = Application.Current.Resources["DividerBrush"] as System.Windows.Media.Brush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4),
            };

            var menuTemplate = new ControlTemplate(typeof(ContextMenu));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BackgroundProperty) });
            borderFactory.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BorderBrushProperty) });
            borderFactory.SetBinding(Border.BorderThicknessProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BorderThicknessProperty) });
            borderFactory.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
            borderFactory.SetValue(Border.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                Direction = 270,
                ShadowDepth = 4,
                BlurRadius = 12,
                Opacity = 0.3
            });
            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
            borderFactory.AppendChild(itemsPresenterFactory);
            menuTemplate.VisualTree = borderFactory;
            menu.Template = menuTemplate;

            var fg = Application.Current.Resources["PrimaryTextBrush"] as System.Windows.Media.Brush;
            var divBrush = Application.Current.Resources["DividerBrush"] as System.Windows.Media.Brush;

            MenuItem MakeItem(string header, ICommand command)
            {
                var item = new MenuItem
                {
                    Header = header,
                    Command = command,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Foreground = fg,
                };

                var itemTemplate = new ControlTemplate(typeof(MenuItem));
                var bd = new FrameworkElementFactory(typeof(Border));
                bd.Name = "Bd";
                bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                bd.SetValue(Border.PaddingProperty, new Thickness(12, 3, 12, 3));
                bd.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(5));

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetBinding(ContentPresenter.ContentProperty,
                    new System.Windows.Data.Binding(nameof(MenuItem.Header))
                    { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
                bd.AppendChild(cp);
                itemTemplate.VisualTree = bd;

                var hlTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                hlTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                    Application.Current.Resources.Contains("ListItemSelectedBackgroundBrush")
                        ? Application.Current.Resources["ListItemSelectedBackgroundBrush"]
                        : System.Windows.Media.Brushes.LightGray, "Bd"));
                itemTemplate.Triggers.Add(hlTrigger);

                var disabledTrigger = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
                disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
                itemTemplate.Triggers.Add(disabledTrigger);

                item.Template = itemTemplate;
                return item;
            }

            menu.Items.Add(MakeItem("复制", ApplicationCommands.Copy));
            menu.Items.Add(new Separator { Margin = new Thickness(0, 2, 0, 2), Background = divBrush });
            menu.Items.Add(MakeItem("全选", ApplicationCommands.SelectAll));

            return menu;
        }

    }
}
