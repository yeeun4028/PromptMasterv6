using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;

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

        // ★★★ 新增：智能有序列表自动编号逻辑 ★★★
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 仅在按下回车键时触发
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                // 1. 获取光标所在行的文本
                int caretIndex = textBox.CaretIndex;
                int lineIndex = textBox.GetLineIndexFromCharacterIndex(caretIndex);
                if (lineIndex < 0) return;

                string lineText = textBox.GetLineText(lineIndex);

                // 2. 正则匹配：以 "数字. " 开头的行 (支持缩进)
                // Pattern: ^(\s*) -> 捕获缩进
                //          (\d+)  -> 捕获数字
                //          \.     -> 匹配点
                //          (\s+)  -> 捕获点后面的空格
                var match = Regex.Match(lineText, @"^(\s*)(\d+)\.(\s+)");

                if (match.Success)
                {
                    // 3. 计算下一个序号
                    string indentation = match.Groups[1].Value;
                    int currentNumber = int.Parse(match.Groups[2].Value);
                    string spacing = match.Groups[3].Value;

                    int nextNumber = currentNumber + 1;
                    string insertText = $"\n{indentation}{nextNumber}.{spacing}";

                    // 4. 插入文本
                    // 使用 SelectedText 插入可以保留 Undo/Redo 栈，且体验更流畅
                    int lineEndIndex = textBox.GetCharacterIndexFromLineIndex(lineIndex) + lineText.Length;

                    // 如果光标不在行尾，暂时不处理（避免打断中间编辑），或者您可以选择强制换行
                    // 这里我们简单处理：直接在光标处插入新行和编号
                    textBox.SelectedText = insertText;
                    textBox.CaretIndex += insertText.Length;

                    // 5. 阻止默认的 Enter 换行，因为我们已经手动插入了带编号的换行
                    e.Handled = true;
                }
            }
        }
    }
}