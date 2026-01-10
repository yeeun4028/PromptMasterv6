using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text; // 引用 StringBuilder

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

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

        // 智能有序列表逻辑
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

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

                    int lineEndIndex = textBox.GetCharacterIndexFromLineIndex(lineIndex) + lineText.Length;

                    textBox.SelectedText = insertText;
                    textBox.CaretIndex += insertText.Length;
                    e.Handled = true;
                }
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

        // ★★★ 新增：智能热键录制逻辑 ★★★
        // ★★★ 修复版：完美支持 Alt 组合键 ★★★
        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 1. 获取真实按键：如果是 System 键（Alt组合），就取 SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // 2. 忽略单纯的修饰键（Ctrl, Alt, Shift, Win）
            // 如果按下的“真实按键”本身就是控制键，则忽略，等待用户按下具体字母
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            e.Handled = true; // 阻止默认输入

            var sb = new StringBuilder();

            // 3. 检测修饰键状态 (ModifierKeys)
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");

            // 4. 追加主键
            sb.Append(key.ToString());

            // 5. 更新到 ViewModel
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                ViewModel.Config.GlobalHotkey = sb.ToString();
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }
        }
    }
}