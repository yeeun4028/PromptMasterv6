using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Models;

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Models.InputMode;
using Button = System.Windows.Controls.Button;
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
                    // ★ 修复：添加 _ = 弃元，消除潜在的 CS4014 警告
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
            }
        }

        // ============================================
        // ★ 高度动态计算逻辑 (支持 Auto-Expand) ★
        // ============================================

        private void MiniInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                UpdateMiniHeight();
            }
        }

        private void UpdateMiniHeight()
        {
            if (MiniInputBox == null) return;

            // 1. 获取内容真实高度，设定最小高度限制
            double contentHeight = MiniInputBox.ExtentHeight;
            if (contentHeight < 40) contentHeight = 40;

            // 2. 限制最大高度，防止条状窗口过长
            if (contentHeight > 400) contentHeight = 400;

            // 3. 计算边框冗余和变量填充区高度
            double chromePadding = 48; // 增加冗余防止遮挡
            double varsHeight = 0;

            // 若有变量需要填写，展开高度
            if (ViewModel.IsMiniVarsExpanded)
            {
                varsHeight = ViewModel.Variables.Count * 45 + 20;
            }

            // 4. 应用最终高度
            this.Height = contentHeight + chromePadding + varsHeight;
        }

        private void EnsureWindowOnScreen()
        {
            if (this.Left < 0) this.Left = 0;
            if (this.Top < 0) this.Top = 0;
        }

        private void MiniWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }
        private void FullWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }

        // =========================================================
        // ★ 极简模式按键逻辑 (CS4014 修复重点)
        // =========================================================

        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter)
            {
                // 搜索确认逻辑
                if (ViewModel.IsSearchPopupOpen)
                {
                    ViewModel.ConfirmSearchResultCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // ★ 修复：添加 _ = 弃元，强制刷新高度防止遮挡
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

                    // 使用 await 等待异步任务
                    await ViewModel.SendByCoordinate();
                    return;
                }

                // Ctrl + Enter 检测 (智能回退)
                if (isCtrl)
                {
                    e.Handled = true;

                    // 使用 await 等待异步任务
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
        // ★ 完整模式逻辑处理 (CS4014 修复重点)
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
                    // await 调用 Task 方法
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                    return;
                }

                if (span < 500)
                {
                    e.Handled = true;
                    // await 调用 Task 方法
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
    }
}