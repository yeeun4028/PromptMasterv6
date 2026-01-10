using System.Windows;
// ★★★ 关键修复：明确指定使用 WPF 版本的 MessageBox，解决与 WinForms 的冲突 ★★★
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5
{
    public partial class NameInputDialog : Window
    {
        public string ResultName { get; private set; } = string.Empty;

        public NameInputDialog(string currentName = "")
        {
            InitializeComponent();

            // 初始化输入框内容
            InputBox.Text = currentName;

            // 自动聚焦并全选文字（替代了 XAML 中不生效的 SelectAllOnFocus 属性）
            InputBox.Focus();
            InputBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 校验是否为空
            if (string.IsNullOrWhiteSpace(InputBox.Text))
            {
                MessageBox.Show("名称不能为空", "提示");
                return;
            }

            // 保存结果并关闭窗口
            ResultName = InputBox.Text.Trim();
            DialogResult = true;
        }
    }
}