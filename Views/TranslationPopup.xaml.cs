using System.Windows;

namespace PromptMasterv5.Views
{
    public partial class TranslationPopup : Window
    {
        public TranslationPopup(string initialText)
        {
            InitializeComponent();
            
            ResultBox.Text = initialText;
            
            // 使用 Loaded 事件确保窗口尺寸已经计算完成
            this.Loaded += TranslationPopup_Loaded;
            
            // 失去焦点自动关闭
            this.Deactivated += (s, e) => this.Close();
        }

        private void TranslationPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // 跟随鼠标位置显示，但确保不超出屏幕
            // 获取当前屏幕的 DPI 缩放比例
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 获取鼠标物理坐标
            var mousePhysical = System.Windows.Forms.Cursor.Position;

            // 转换为逻辑坐标 (WPF 使用的坐标系)
            double mouseX = mousePhysical.X / dpiX;
            double mouseY = mousePhysical.Y / dpiY;
            
            // 获取工作区域（排除任务栏）
            var workArea = SystemParameters.WorkArea;
            
            // 获取窗口实际尺寸
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;
            
            // 如果实际尺寸为0，使用期望尺寸
            if (windowWidth == 0 || windowHeight == 0)
            {
                this.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                windowWidth = this.DesiredSize.Width;
                windowHeight = this.DesiredSize.Height;
            }
            
            // 默认显示在鼠标右下方
            double left = mouseX + 15;
            double top = mouseY + 15;

            // 独立检查水平方向：如果超出右边界，则显示在鼠标左侧
            if (left + windowWidth > workArea.Right)
            {
                left = mouseX - windowWidth - 15;
            }

            // 独立检查垂直方向：如果超出下边界，则显示在鼠标上方
            if (top + windowHeight > workArea.Bottom)
            {
                top = mouseY - windowHeight - 15;
            }
            
            // 最终确保窗口完全在工作区域内
            left = System.Math.Max(workArea.Left, System.Math.Min(left, workArea.Right - windowWidth));
            top = System.Math.Max(workArea.Top, System.Math.Min(top, workArea.Bottom - windowHeight));
            
            this.Left = left;
            this.Top = top;
        }

        public void UpdateText(string text)
        {
            ResultBox.Text = text;
        }

    }
}