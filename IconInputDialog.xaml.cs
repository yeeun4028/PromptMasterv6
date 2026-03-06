using System;
using System.Windows;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv6
{
    public partial class IconInputDialog : Window
    {
        // 用来把输入的图标代码传回给主界面
        public string ResultGeometry { get; private set; } = string.Empty;

        // ★ 修复点：允许传入 null，并在内部处理
        public IconInputDialog(string? currentGeometry = "")
        {
            InitializeComponent();

            string initialText = currentGeometry ?? "";
            InputBox.Text = initialText;

            // 自动聚焦到输入框
            Loaded += (_, _) => InputBox.Focus();

            UpdatePreview(initialText);
        }

        private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePreview(InputBox.Text);
        }

        private void UpdatePreview(string data)
        {
            if (PreviewPath == null || PreviewBorder == null) return;

            if (string.IsNullOrWhiteSpace(data))
            {
                // 空输入：清空预览，恢复默认边框
                PreviewPath.Data = null;
                PreviewHint.Visibility = Visibility.Visible;
                ErrorHint.Visibility = Visibility.Collapsed;
                PreviewBorder.BorderBrush = FindResource("DividerBrush") as System.Windows.Media.Brush;
            }
            else
            {
                try
                {
                    // 尝试解析 SVG 代码
                    PreviewPath.Data = Geometry.Parse(data);
                    // 解析成功：绿色边框，隐藏提示
                    PreviewHint.Visibility = Visibility.Collapsed;
                    ErrorHint.Visibility = Visibility.Collapsed;
                    PreviewBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)); // #4CAF50 success green
                }
                catch
                {
                    // 解析失败：清空预览，红色边框 + 错误提示
                    PreviewPath.Data = null;
                    PreviewHint.Visibility = Visibility.Collapsed;
                    ErrorHint.Visibility = Visibility.Visible;
                    PreviewBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35)); // #E53935 error red
                }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存前校验
                if (!string.IsNullOrWhiteSpace(InputBox.Text))
                {
                    Geometry.Parse(InputBox.Text);
                }
                ResultGeometry = InputBox.Text;
                DialogResult = true;
            }
            catch
            {
                MessageBox.Show("图标代码格式不正确，无法生成预览，请检查。", "格式错误");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Text = "";
        }
    }
}