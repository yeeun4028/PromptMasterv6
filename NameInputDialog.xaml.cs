using System.Windows;
using System.Windows.Controls;
// ★★★ 关键修复：明确指定使用 WPF 版本的 MessageBox，解决与 WinForms 的冲突 ★★★
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv6
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

            // 根据初始内容决定确定按钮状态
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(currentName);
        }

        /// <summary>
        /// 实时监听输入框内容变化，动态启用/禁用"确定"按钮
        /// </summary>
        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (OkButton != null)
            {
                OkButton.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 校验是否为空 (双重保险, 按钮已在 UI 层控制)
            if (string.IsNullOrWhiteSpace(InputBox.Text))
            {
                return;
            }

            // 保存结果并关闭窗口
            ResultName = InputBox.Text.Trim();
            DialogResult = true;
        }
    }
}