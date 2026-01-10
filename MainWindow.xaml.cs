using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Models;

// 解决控件引用歧义
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using WinFormsCursor = System.Windows.Forms.Cursor;

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        // 用于检测双击 Enter
        private DateTime _lastEnterTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // ★★★ 核心方法提取：统一的发送处理流程 ★★★
        private void TriggerSendProcess(TextBox sourceBox)
        {
            // 1. 强制更新绑定 (所见即所得，防止输入法未提交)
            sourceBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            // 2. 强制隐藏窗口 (用户体验第一位)
            this.Hide();

            // 3. 执行发送命令
            // 注意：如果是变量模式，ViewModel 会自动读取 Variables 集合；
            // 如果是附加输入模式，ViewModel 会读取 AdditionalInput 属性。
            ViewModel.SendCombinedInputCommand.Execute(null);
        }

        // BLOCK3: 变量输入框的按键逻辑
        // ★★★ 修复点：在此处增加了 Ctrl+Enter 检测 ★★★
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                // 1. 检测 Ctrl + Enter (新增功能)
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    TriggerSendProcess(textBox);
                    return;
                }

                // 2. 原有的智能有序列表逻辑 (保持不变)
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
        }

        // BLOCK4: 附加输入框的按键逻辑
        private void AdditionalInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                // 判断是否是 Ctrl+Enter
                bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                // 判断是否是双击 Enter (500ms 内连按)
                var now = DateTime.Now;
                var span = (now - _lastEnterTime).TotalMilliseconds;
                bool isDoubleEnter = span < 500;

                if (isCtrlEnter || isDoubleEnter)
                {
                    e.Handled = true; // 阻止换行符输入

                    // ★★★ 调用统一的发送流程 ★★★
                    // 额外动作：对于 BLOCK4，我们手动同步一下属性，双重保险
                    ViewModel.AdditionalInput = textBox.Text;
                    TriggerSendProcess(textBox);

                    if (isDoubleEnter) _lastEnterTime = DateTime.MinValue;
                }
                else
                {
                    _lastEnterTime = now;
                }
            }
        }

        // 列表项双击发送
        private void FileListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem)
            {
                ViewModel.SendDirectPromptCommand.Execute(null);
            }
        }

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox != null && ViewModel.Config != null)
            {
                if (passwordBox.Password != ViewModel.Config.Password)
                {
                    passwordBox.Password = ViewModel.Config.Password;
                }
            }
        }

        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox != null && ViewModel.Config != null)
            {
                ViewModel.Config.Password = passwordBox.Password;
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            e.Handled = true;

            var sb = new StringBuilder();

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");

            sb.Append(key.ToString());

            var textBox = sender as TextBox;
            if (textBox != null)
            {
                ViewModel.Config.GlobalHotkey = sb.ToString();
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }
        }

        // 坐标拾取逻辑
        private async void PickCoordinate_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            string originalContent = btn.Content.ToString() ?? "拾取";

            try
            {
                btn.IsEnabled = false;
                for (int i = 3; i > 0; i--)
                {
                    btn.Content = $"{i}";
                    await Task.Delay(1000);
                }

                var pt = WinFormsCursor.Position;
                ViewModel.LocalConfig.ClickX = pt.X;
                ViewModel.LocalConfig.ClickY = pt.Y;

                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }
    }
}