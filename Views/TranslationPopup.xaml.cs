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
        }

        private void TranslationPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // 跟随鼠标位置显示，但确保不超出屏幕
            var mouse = System.Windows.Forms.Cursor.Position;
            
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
            
            // 默认显示在鼠标右下方，偏移一点距离
            double left = mouse.X + 15;
            double top = mouse.Y + 15;
            
            // 检查右边界，如果超出则显示在鼠标左侧
            if (left + windowWidth > workArea.Right)
            {
                left = mouse.X - windowWidth - 15;
            }
            
            // 检查下边界，如果超出则显示在鼠标上方
            if (top + windowHeight > workArea.Bottom)
            {
                top = mouse.Y - windowHeight - 15;
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