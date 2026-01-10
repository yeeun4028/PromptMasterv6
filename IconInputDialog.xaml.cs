using System;
using System.Windows;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5
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
            UpdatePreview(initialText);
        }

        private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePreview(InputBox.Text);
        }

        private void UpdatePreview(string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data))
                {
                    PreviewPath.Data = null;
                }
                else
                {
                    // 尝试解析 SVG 代码
                    PreviewPath.Data = Geometry.Parse(data);
                }
            }
            catch
            {
                // 解析失败不显示，避免报错
                PreviewPath.Data = null;
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