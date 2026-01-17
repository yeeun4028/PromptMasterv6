using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Models;
using PromptMasterv5.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq; // 新增引用，用于查询子窗口状态

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Models.InputMode;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using WinFormsCursor = System.Windows.Forms.Cursor;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        private double _lastFullWidth = 1000;
        private double _lastFullHeight = 600;
        private double _lastFullLeft = 100;
        private double _lastFullTop = 100;

        private DateTime _lastMiniEnterTime = DateTime.MinValue;
        private DateTime _lastVarEnterTime = DateTime.MinValue;
        private DateTime _lastAddEnterTime = DateTime.MinValue;
        private int _miniEnterSequence = 0;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;
        private DispatcherTimer? _hideTimer;

        public MainWindow()
        {
            InitializeComponent();

            // 设置窗口不在任务栏显示
            this.ShowInTaskbar = false;

            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            InitializeTrayIcon();
            ApplyModeState();

            // 处理窗口关闭事件
            this.Closing += MainWindow_Closing;
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

                // 添加托盘菜单
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("显示/隐藏窗口", null, (s, e) => ToggleWindowVisibility());
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add("退出", null, (s, e) =>
                {
                    _isExiting = true;
                    this.Close();
                });

                _notifyIcon.ContextMenuStrip = contextMenu;

                // 单击托盘图标显示/隐藏窗口
                _notifyIcon.Click += (s, e) =>
                {
                    if (e is System.Windows.Forms.MouseEventArgs mouseArgs && mouseArgs.Button == System.Windows.Forms.MouseButtons.Left)
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

        private void ToggleWindowVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                // 取消隐藏定时器
                StopHideTimer();

                this.Show();
                this.Activate();
                this.Focus();

                // 唤醒时强制置顶
                this.Topmost = true;
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // ★★★ 新增：安全退出拦截机制 ★★★
            if (ViewModel.IsDirty)
            {
                var result = MessageBox.Show("您有未备份的修改。是否在退出/隐藏前备份到云端？", "未保存的更改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // 取消关闭/隐藏操作
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    // 同步执行备份
                    ViewModel.ManualBackupCommand.Execute(null);
                }
                // 如果选 No，直接继续执行后续关闭或隐藏操作
            }

            // 如果不是通过退出菜单关闭，则隐藏窗口而不是关闭
            if (!_isExiting)
            {
                e.Cancel = true;  // 取消关闭事件
                this.Hide();       // 隐藏窗口
                return;
            }

            // 清理托盘图标
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // 1. 窗口被激活（唤醒）时，取消任何待执行的隐藏操作
            StopHideTimer();

            // 2. 需求实现：唤醒时置顶显示
            // 无论是极简还是完整模式，只要被激活，就应该在最上层
            this.Topmost = true;

            // 3. 强制抢占前台焦点 (Win32 API)
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            // 4. 处理极简模式下的输入框焦点
            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MiniInputBox != null)
                    {
                        MiniInputBox.Focus();
                        Keyboard.Focus(MiniInputBox);
                        MiniInputBox.CaretIndex = MiniInputBox.Text.Length;
                    }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (ViewModel != null && ViewModel.LocalConfig.IsMiniTopmostLocked)
            {
                return;
            }

            // 1. 需求实现：不需要始终保持置顶
            // 当失去焦点（用户点击了其他软件）时，取消置顶，允许其他窗口覆盖它
            this.Topmost = false;

            // 取消之前的定时器
            StopHideTimer();

            // 2. 需求实现：失去焦点自动隐藏
            // 创建新的定时器，延迟200毫秒后检查并隐藏窗口
            // 延迟是为了防止在应用内部切换焦点（如点击弹出菜单、对话框）时误判为失焦
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(200);
            _hideTimer.Tick += (s, args) =>
            {
                StopHideTimer();

                // ★★★ 核心修复：检测子窗口状态 ★★★
                // 如果当前应用程序的任何窗口（包括对话框）处于激活状态，则视为未完全失焦，不隐藏
                bool isAnyChildActive = Application.Current.Windows.Cast<Window>().Any(w => w.IsActive);

                // 只有当主窗口不活动，且没有任何子窗口活动时，才执行隐藏
                if (!this.IsActive && !isAnyChildActive)
                {
                    this.Hide();
                }
            };
            _hideTimer.Start();
        }

        private void StopHideTimer()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer = null;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullMode))
            {
                ApplyModeState();
            }
            else if (e.PropertyName == nameof(MainViewModel.IsMiniVarsExpanded))
            {
                if (!ViewModel.IsFullMode) UpdateMiniHeight();
            }
            else if (e.PropertyName == nameof(MainViewModel.MiniInputText))
            {
                if (string.IsNullOrEmpty(ViewModel.MiniInputText) && !ViewModel.IsFullMode)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() => {
                        UpdateMiniHeight();
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
            }
        }

        private void ApplyModeState()
        {
            if (ViewModel.IsFullMode)
            {
                this.Width = _lastFullWidth;
                this.Height = _lastFullHeight;
                this.Left = _lastFullLeft;
                this.Top = _lastFullTop;
                this.ResizeMode = ResizeMode.CanResize;

                // 切换模式时，确保重置Topmost
                this.Topmost = true;
                EnsureWindowOnScreen();
            }
            else
            {
                _lastFullWidth = this.Width;
                _lastFullHeight = this.Height;
                _lastFullLeft = this.Left;
                _lastFullTop = this.Top;

                this.Width = 600;
                UpdateMiniHeight();

                var sw = SystemParameters.PrimaryScreenWidth;
                var sh = SystemParameters.PrimaryScreenHeight;
                this.Left = (sw - this.Width) / 2;
                this.Top = sh * 0.2;

                this.ResizeMode = ResizeMode.CanResize;

                // 切换模式时，确保重置Topmost并激活
                this.Topmost = true;
                this.Activate();
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

                _ = Dispatcher.BeginInvoke(new Action(() => MiniInputBox.Focus()), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void MiniInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MiniInputBox != null)
            {
                var text = MiniInputBox.Text?.Trim() ?? "";
                var isUrlLike = Regex.IsMatch(text, @"^(https?://|www\.)\S+$", RegexOptions.IgnoreCase);
                if (!isUrlLike && Uri.TryCreate(text, UriKind.Absolute, out var uri))
                {
                    isUrlLike = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
                }

                if (isUrlLike)
                {
                    var accent = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
                    MiniInputBox.Foreground = accent ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x41, 0x83, 0xC4));
                }
                else
                {
                    MiniInputBox.ClearValue(TextBox.ForegroundProperty);
                }
            }

            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateMiniHeight();
                    if (MiniInputBox.LineCount > 0)
                    {
                        var caretIndex = MiniInputBox.CaretIndex;
                        var lineIndex = MiniInputBox.GetLineIndexFromCharacterIndex(caretIndex);
                        if (lineIndex >= 0 && lineIndex < MiniInputBox.LineCount)
                        {
                            MiniInputBox.ScrollToLine(lineIndex);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private childItem? FindVisualChild<childItem>(DependencyObject? obj) where childItem : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is childItem item) return item;
                childItem? childOfChild = FindVisualChild<childItem>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void UpdateMiniHeight()
        {
            if (MiniInputBox == null) return;
            double contentHeight = MiniInputBox.ExtentHeight;
            if (contentHeight < 40) contentHeight = 40;
            double chromePadding = 60;
            double varsHeight = 0;
            if (ViewModel.IsMiniVarsExpanded)
            {
                double estimatedVarsHeight = ViewModel.Variables.Count * 45 + 20;
                if (estimatedVarsHeight > 300) estimatedVarsHeight = 300;
                varsHeight = estimatedVarsHeight;
            }
            double targetHeight = contentHeight + chromePadding + varsHeight;
            double screenHalf = SystemParameters.PrimaryScreenHeight / 2;
            if (targetHeight > screenHalf) targetHeight = screenHalf;
            double workAreaTop = SystemParameters.WorkArea.Top;
            double workAreaBottom = SystemParameters.WorkArea.Bottom;
            double currentTop = this.Top;
            double safetyMargin = 20;
            double predictedBottom = currentTop + targetHeight + safetyMargin;
            if (predictedBottom > workAreaBottom)
            {
                double offset = predictedBottom - workAreaBottom;
                double newTop = currentTop - offset;
                if (newTop < workAreaTop)
                {
                    newTop = workAreaTop;
                    double maxPossibleHeight = workAreaBottom - workAreaTop - safetyMargin;
                    if (targetHeight > maxPossibleHeight) targetHeight = maxPossibleHeight;
                }
                this.Top = newTop;
            }
            this.Height = targetHeight;
            if (MiniInputBox.IsKeyboardFocused && MiniInputBox.LineCount > 0)
            {
                var caretIndex = MiniInputBox.CaretIndex;
                var lineIndex = MiniInputBox.GetLineIndexFromCharacterIndex(caretIndex);
                if (lineIndex >= 0 && lineIndex < MiniInputBox.LineCount)
                {
                    MiniInputBox.ScrollToLine(lineIndex);
                }
            }
        }

        private void EnsureWindowOnScreen()
        {
            if (this.Left < 0) this.Left = 0;
            if (this.Top < 0) this.Top = 0;
        }

        private void MiniWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }
        private void FullWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }

        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                return;
            }

            if (e.Key == Key.Delete && ViewModel.LocalConfig.MiniAiOnlyChatEnabled)
            {
                ViewModel.MiniInputText = "";
                ViewModel.IsAiResultDisplayed = false;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && ViewModel.IsAiResultDisplayed)
            {
                ViewModel.MiniInputText = "";
                ViewModel.IsAiResultDisplayed = false;
                e.Handled = true;
                return;
            }

            if (ViewModel.IsSearchPopupOpen && ViewModel.SearchResults.Count > 0)
            {
                if (e.Key == Key.Down)
                {
                    int newIndex = SearchListBox.SelectedIndex + 1;
                    if (newIndex >= ViewModel.SearchResults.Count) newIndex = 0;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Up)
                {
                    int newIndex = SearchListBox.SelectedIndex - 1;
                    if (newIndex < 0) newIndex = ViewModel.SearchResults.Count - 1;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                if (ViewModel.IsSearchPopupOpen)
                {
                    ViewModel.ConfirmSearchResultCommand.Execute(null);
                    e.Handled = true;
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateMiniHeight();
                        if (ViewModel.HasVariables)
                        {
                            var container = MiniVarsList.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                            if (container != null)
                            {
                                var firstBox = FindVisualChild<TextBox>(container);
                                if (firstBox != null)
                                {
                                    firstBox.Focus();
                                    firstBox.SelectAll();
                                    return;
                                }
                            }
                        }
                        MiniInputBox.Focus();
                        MiniInputBox.CaretIndex = MiniInputBox.Text.Length;
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                    return;
                }

                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastMiniEnterTime).TotalMilliseconds;

                if (isCtrl)
                {
                    e.Handled = true;
                    _miniEnterSequence++;
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                    return;
                }

                if (!ViewModel.LocalConfig.MiniAiOnlyChatEnabled)
                {
                    e.Handled = true;
                    _miniEnterSequence++;
                    await TriggerSendProcess(textBox, ViewModel.LocalConfig.Mode);
                    return;
                }

                if (span < 500)
                {
                    e.Handled = true;
                    _lastMiniEnterTime = DateTime.MinValue;
                    _miniEnterSequence++;
                    await TriggerSendProcess(textBox, InputMode.CoordinateClick);
                    return;
                }

                e.Handled = true;
                _lastMiniEnterTime = now;
                var enterSeq = ++_miniEnterSequence;

                _ = Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(450);
                    if (_miniEnterSequence != enterSeq) return;
                    await ViewModel.ExecuteMiniAiOrPatternAsync();
                    UpdateMiniHeight();
                    MiniInputBox.Focus();
                    MiniInputBox.CaretIndex = MiniInputBox.Text.Length;
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ViewModel.EnterFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e) => ViewModel.ConfirmSearchResultCommand.Execute(null);

        private async Task TriggerSendProcess(TextBox sourceBox, InputMode mode)
        {
            sourceBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            this.Hide();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            await Task.Delay(60);
            if (mode == InputMode.SmartFocus)
                await ViewModel.SendBySmartFocus();
            else
                await ViewModel.SendByCoordinate();
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
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                    return;
                }
                if (span < 500)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.CoordinateClick);
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
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                }
                else if (span < 500)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.CoordinateClick);
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

        private async void FileListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem) await ViewModel.SendBySmartFocus();
        }

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null && pb.Password != ViewModel.Config.Password) pb.Password = ViewModel.Config.Password; }
        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null) ViewModel.Config.Password = pb.Password; }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async void PickCoordinate_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return; string org = btn.Content.ToString() ?? "拾取";
            try
            {
                btn.IsEnabled = false;
                for (int i = 3; i > 0; i--) { btn.Content = $"{i}"; await Task.Delay(1000); }
                var pt = WinFormsCursor.Position;
                ViewModel.LocalConfig.ClickX = pt.X;
                ViewModel.LocalConfig.ClickY = pt.Y;
                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally { btn.Content = org; btn.IsEnabled = true; }
        }


        private async void TestAiConnection_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var statusText = this.FindName("TestStatusText") as TextBlock;
            if (statusText == null) return;

            try
            {
                statusText.Text = "🔄 测试中...";
                statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
                btn.IsEnabled = false;

                var aiService = new AiService();
                (bool success, string message) = await aiService.TestConnectionAsync(ViewModel.Config);

                if (success)
                {
                    statusText.Text = "✅ 成功连通";
                    statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 160, 71));

                    await Task.Delay(3000);
                    statusText.Text = "";
                }
                else
                {
                    statusText.Text = "❌ 连通失败";
                    statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53));
                }
            }
            catch (Exception)
            {
                statusText.Text = "❌ 连接异常";
                statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53));
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void AiModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (listBox.SelectedItem is AiModelConfig selectedModel)
            {
                ViewModel.ActivateAiModelCommand.Execute(selectedModel);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (ViewModel != null)
                {
                    ViewModel.LocalConfig.IsMiniTopmostLocked = false;
                }
                ViewModel.ExitFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
