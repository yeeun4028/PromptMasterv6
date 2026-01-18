using System.Windows;

namespace PromptMasterv5.Views
{
    public partial class TranslationPopup : Window
    {
        public TranslationPopup(string initialText)
        {
            InitializeComponent();
            ResultBox.Text = initialText;
            // 跟随鼠标位置显示
            var mouse = System.Windows.Forms.Cursor.Position;
            // 简单的防溢出处理
            double left = mouse.X + 15;
            double top = mouse.Y + 15;

            if (left + Width > SystemParameters.VirtualScreenWidth) left = mouse.X - Width - 10;
            if (top + Height > SystemParameters.VirtualScreenHeight) top = mouse.Y - Height - 10;

            this.Left = left;
            this.Top = top;
        }

        public void UpdateText(string text)
        {
            ResultBox.Text = text;
        }

        private void Window_Deactivated(object sender, System.EventArgs e)
        {
            // 失去焦点自动关闭
            this.Close();
        }
    }
}