using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text;
using System.Windows.Media; // 用于 FindVisualChild
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Models;

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Models.InputMode;
using Button = System.Windows.Controls.Button;
// ★ 明确指定 TextBox 别名，防止歧义
using TextBox = System.Windows.Controls.TextBox;
using WinFormsCursor = System.Windows.Forms.Cursor;

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        // 记忆完整模式的窗口状态
        private double _lastFullWidth = 1000;
        private double _lastFullHeight = 600;
        private double _lastFullLeft = 100;
        private double _lastFullTop = 100;

        // 输入区域按键判定计时器
        private DateTime _lastMiniEnterTime = DateTime.MinValue;
        private DateTime _lastVarEnterTime = DateTime.MinValue;
        private DateTime _lastAddEnterTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;

            // 监听 ViewModel 状态变化
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ApplyModeState();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullMode))
            {
                ApplyModeState();
            }
            else if (e.PropertyName == nameof(MainViewModel.IsMiniVarsExpanded))
            {
                // 仅在极简模式且变量区域变动时更新高度
                if (!ViewModel.IsFullMode) UpdateMiniHeight();
            }
            // 监听输入内容清空（发送后），并在渲染完成后还原高度
            else if (e.PropertyName == nameof(MainViewModel.MiniInputText))
            {
                if (string.IsNullOrEmpty(ViewModel.MiniInputText) && !ViewModel.IsFullMode)
                {
                    // 使用 Dispatcher + 弃元，消除 CS4014
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
                // === 完整模式：恢复尺寸并取消置顶 ===
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
                // === 极简模式：保存当前状态并设为条状置顶 ===
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

                // 切换回极简模式后强制聚焦输入框
                _ = Dispatcher.BeginInvoke(new Action(() => MiniInputBox.Focus()), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        // ============================================
        // ★ 高度动态计算逻辑 (防穿透屏幕底部 & 自动滚动) ★
        // ============================================

        private void MiniInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                UpdateMiniHeight();

                // ★ 修复：改用 ScrollToLine 替代 ScrollToCaret
                if (MiniInputBox.LineCount > 0)
                {
                    var caretIndex = MiniInputBox.CaretIndex;
                    var lineIndex = MiniInputBox.GetLineIndexFromCharacterIndex(caretIndex);
                    // 防止越界
                    if (lineIndex >= 0 && lineIndex < MiniInputBox.LineCount)
                    {
                        MiniInputBox.ScrollToLine(lineIndex);
                    }
                }
            }
        }

        // 辅助方法：用于在 ItemsControl 中查找 TextBox (增加可空引用支持)
        private childItem? FindVisualChild<childItem>(DependencyObject? obj) where childItem : DependencyObject
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child is childItem item)
                    return item;

                childItem? childOfChild = FindVisualChild<childItem>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void UpdateMiniHeight()
        {
            if (MiniInputBox == null) return;

            // 1. 获取内容真实高度，设定最小高度限制
            double contentHeight = MiniInputBox.ExtentHeight;
            if (contentHeight < 40) contentHeight = 40;

            // 2. 计算最大可用高度
            // 限制A: 屏幕高度的一半 (保持设计初衷)
            double screenHalf = SystemParameters.PrimaryScreenHeight / 2;

            // 限制B: 当前位置到底部的距离 (防止穿透屏幕底部)
            // 减去 50px 作为安全边距(Taskbar缓冲)
            double distToBottom = SystemParameters.WorkArea.Height - this.Top - 50;
            if (distToBottom < 100) distToBottom = 100; // 至少保留100px显示空间

            // 最终最大高度 = A 和 B 中较小的一个
            double finalMaxHeight = Math.Min(screenHalf, distToBottom);

            // 3. 应用限制
            if (contentHeight > finalMaxHeight) contentHeight = finalMaxHeight;

            // 4. 计算变量区域高度
            double chromePadding = 60;
            double varsHeight = 0;

            if (ViewModel.IsMiniVarsExpanded)
            {
                double estimatedVarsHeight = ViewModel.Variables.Count * 45 + 20;
                // 变量区也要受限，防止把输入框挤没了，最多占剩余空间的 40%
                double maxVars = finalMaxHeight * 0.4;
                if (estimatedVarsHeight > 300) estimatedVarsHeight = 300; // 绝对上限

                varsHeight = estimatedVarsHeight;
            }

            // 5. 应用最终窗口高度
            this.Height = contentHeight + chromePadding + varsHeight;

            // ★ 修复：改用 ScrollToLine 替代 ScrollToCaret
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
        // ★ 极简模式按键逻辑 (含键盘导航与智能焦点)
        // =========================================================

        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 搜索列表的键盘导航 (上/下键选择)
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
                // 搜索确认后的智能焦点流转
                if (ViewModel.IsSearchPopupOpen)
                {
                    // 1. 执行确认命令 (填入文本)
                    ViewModel.ConfirmSearchResultCommand.Execute(null);

                    // 2. 标记事件已处理
                    e.Handled = true;

                    // 3. 等待 UI 更新 (变量列表生成需要时间)
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 强制刷新高度
                        UpdateMiniHeight();

                        if (ViewModel.HasVariables)
                        {
                            // 场景 A：有变量 -> 聚焦第一个变量输入框
                            // 利用 ItemsControl 的 ItemContainerGenerator 找到第一个容器
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

                        // 场景 B：无变量 (或查找失败) -> 聚焦主输入框并移到末尾
                        MiniInputBox.Focus();
                        MiniInputBox.CaretIndex = MiniInputBox.Text.Length;

                    }), System.Windows.Threading.DispatcherPriority.ContextIdle); // 使用 ContextIdle 确保数据绑定完成

                    return;
                }

                // --- 发送逻辑 ---

                // 强制刷新高度防止遮挡
                _ = Dispatcher.BeginInvoke(new Action(() => {
                    UpdateMiniHeight();
                }), System.Windows.Threading.DispatcherPriority.Render);

                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastMiniEnterTime).TotalMilliseconds;

                // 双击 Enter 检测 (坐标模式)
                if (span < 500 && !isCtrl)
                {
                    e.Handled = true;
                    _lastMiniEnterTime = DateTime.MinValue;
                    await ViewModel.SendByCoordinate();
                    return;
                }

                // Ctrl + Enter 检测 (智能回退)
                if (isCtrl)
                {
                    e.Handled = true;
                    await ViewModel.SendBySmartFocus();
                    return;
                }

                _lastMiniEnterTime = now;

                // 自动编号逻辑
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
                    int nextNumber = currentNumber + 1;
                    string insertText = $"\n{indentation}{nextNumber}.{spacing}";
                    textBox.SelectedText = insertText;
                    textBox.CaretIndex += insertText.Length;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Ctrl + Up 切换到完整模式
                ViewModel.EnterFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e) => ViewModel.ConfirmSearchResultCommand.Execute(null);

        // =========================================================
        // ★ 完整模式逻辑处理
        // =========================================================

        private async Task TriggerSendProcess(TextBox sourceBox, InputMode mode)
        {
            // 同步 UI 更改到 ViewModel
            sourceBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            this.Hide();

            // 内部调用也必须 await
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
                    // 自动编号
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

        // ESC 键功能：完整模式->极简模式->隐藏
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