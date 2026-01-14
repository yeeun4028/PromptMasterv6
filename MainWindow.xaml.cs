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

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Models.InputMode;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using WinFormsCursor = System.Windows.Forms.Cursor;
using MessageBox = System.Windows.MessageBox;

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

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ApplyModeState();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
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

        // ... (ApplyModeState, MiniInput_TextChanged, FindVisualChild, UpdateMiniHeight 等保持不变) ...
        // 请保留中间这些负责窗口高度计算和拖拽的方法

        private void ApplyModeState()
        {
            if (ViewModel.IsFullMode)
            {
                this.Width = _lastFullWidth;
                this.Height = _lastFullHeight;
                this.Left = _lastFullLeft;
                this.Top = _lastFullTop;
                this.ResizeMode = ResizeMode.CanResize;
                this.Topmost = false;
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
                this.Topmost = true;

                _ = Dispatcher.BeginInvoke(new Action(() => MiniInputBox.Focus()), System.Windows.Threading.DispatcherPriority.Render);
                this.Activate();
            }
        }

        private void MiniInput_TextChanged(object sender, TextChangedEventArgs e)
        {
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

        // =========================================================
        // ★ 核心修改：在按键预览中处理 AI 触发 ★
        // =========================================================

        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 1. 搜索提示列表导航 (Up/Down)
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
                // 2. 确认搜索结果
                if (ViewModel.IsSearchPopupOpen)
                {
                    ViewModel.ConfirmSearchResultCommand.Execute(null);
                    e.Handled = true;
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateMiniHeight();
                        // 如果选中的模版有变量，尝试聚焦第一个变量输入框
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

                // 3. ★★★ AI 触发逻辑 (更新版) ★★★
                // 支持：ai+空格(半角/全角)、''(英文双单引号)、’‘(中文双单引号)
                string text = ViewModel.MiniInputText.Trim();
                if (text.StartsWith("ai ", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("ai　", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("''") ||
                    text.StartsWith("’‘"))
                {
                    e.Handled = true; // 拦截 Enter，不发送，转为执行 AI
                    await ViewModel.ExecuteAiQuery();

                    // AI 返回结果后，更新高度并聚焦末尾
                    await Dispatcher.BeginInvoke(new Action(() => {
                        UpdateMiniHeight();
                        MiniInputBox.Focus();
                        MiniInputBox.CaretIndex = MiniInputBox.Text.Length;
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                    return;
                }

                // 4. 常规发送逻辑
                _ = Dispatcher.BeginInvoke(new Action(() => {
                    UpdateMiniHeight();
                }), System.Windows.Threading.DispatcherPriority.Render);

                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastMiniEnterTime).TotalMilliseconds;

                // 双击 Enter (间隔小于500ms) -> 坐标点击模式发送
                if (span < 500 && !isCtrl)
                {
                    e.Handled = true;
                    _lastMiniEnterTime = DateTime.MinValue;
                    await ViewModel.SendByCoordinate();
                    return;
                }

                // Ctrl + Enter -> 智能焦点回退发送
                if (isCtrl)
                {
                    e.Handled = true;
                    await ViewModel.SendBySmartFocus();
                    return;
                }

                _lastMiniEnterTime = now;

                // 5. 自动列表编号 (1. -> Enter -> 2.)
                int caretIndex = textBox.CaretIndex;
                int lineIndex = textBox.GetLineIndexFromCharacterIndex(caretIndex);
                if (lineIndex >= 0)
                {
                    string lineText = textBox.GetLineText(lineIndex);
                    var match = Regex.Match(lineText, @"^(\s*)(\d+)\.(\s+)");
                    if (match.Success)
                    {
                        string indentation = match.Groups[1].Value;
                        int currentNumber = int.Parse(match.Groups[2].Value);
                        string spacing = match.Groups[3].Value;
                        int nextNumber = currentNumber + 1;
                        string insertText = $"\n{indentation}{nextNumber}.{spacing}";
                        textBox.SelectedText = insertText;
                        textBox.CaretIndex += insertText.Length;
                        e.Handled = true;
                    }
                }
            }
            // 6. Ctrl + Up -> 切换到完整模式
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ViewModel.EnterFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e) => ViewModel.ConfirmSearchResultCommand.Execute(null);

        // ... (TriggerSendProcess, TextBox_PreviewKeyDown, AdditionalInputBox_PreviewKeyDown 等保持不变) ...

        private async Task TriggerSendProcess(TextBox sourceBox, InputMode mode)
        {
            sourceBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            this.Hide();
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

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            if (sender is TextBox tb) { ViewModel.Config.GlobalHotkey = sb.ToString(); tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget(); }
        }
        // ★★★ 修复 CA1416: 显式标记支持 Windows 平台 ★★★
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

            string originalContent = btn.Content?.ToString() ?? "连通测试";

            try
            {
                btn.IsEnabled = false;
                btn.Content = "测试中...";

                var aiService = new AiService();
                (bool success, string message) = await aiService.TestConnectionAsync(ViewModel.Config);

                MessageBox.Show(
                    success ? "✓ " + message : "✗ " + message,
                    "连接测试",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }


        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.ExitFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}