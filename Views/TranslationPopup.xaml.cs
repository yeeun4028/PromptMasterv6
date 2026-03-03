using System.Windows;

namespace PromptMasterv5.Views
{
    public partial class TranslationPopup : Window
    {
        public bool IsClosing { get; private set; }

        public TranslationPopup(string initialText)
        {
            InitializeComponent();
            
            ResultBox.Text = initialText;
            
            // 使用 Loaded 事件确保窗口尺寸已经计算完成
            this.Loaded += TranslationPopup_Loaded;
            
            // 失去焦点自动关闭
            this.Deactivated += (s, e) => 
            {
                if (!IsClosing) Close();
            };

            this.Closing += (s, e) => IsClosing = true;
        }

        private Rect? _placementTarget;

        public void SetPlacementTarget(Rect target)
        {
            _placementTarget = target;
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
            // 如果有选区信息，优先使用选区右下角作为锚点
            double anchorX = mouseX;
            double anchorY = mouseY;
            
            if (_placementTarget.HasValue)
            {
                // 使用选区的右下角代替鼠标坐标
                // 补回虚拟屏幕的偏移，防止副屏选区坐标飞到主屏
                var target = _placementTarget.Value;
                target.X += SystemParameters.VirtualScreenLeft;
                target.Y += SystemParameters.VirtualScreenTop;
                
                anchorX = target.Right;
                anchorY = target.Bottom;

                double left = anchorX + 10;
                double top = anchorY + 10;

                // 智能避让逻辑
                bool overflowRight = (left + windowWidth > workArea.Right);
                bool overflowBottom = (top + windowHeight > workArea.Bottom);

                // 策略 1: 默认 (Target 右下) -> (left, top)
                
                // 策略 2: 如果右边缘溢出 -> 尝试 Target 左下
                // (Target.Left - windowWidth - 10, Target.Bottom + 10)
                if (overflowRight && !overflowBottom)
                {
                    double tryLeft = target.Left - windowWidth - 10;
                    if (tryLeft >= workArea.Left)
                    {
                         left = tryLeft;
                         // top 保持不变
                    }
                    else
                    {
                         // 左边也放不下？那就依然用原来的逻辑（会被强制拉回屏幕内，或者尝试上方）
                    }
                }

                // 策略 3: 如果下边缘溢出 -> 尝试 Target 右上
                // (Target.Right + 10, Target.Top - windowHeight - 10)
                if (!overflowRight && overflowBottom)
                {
                     double tryTop = target.Top - windowHeight - 10;
                     if (tryTop >= workArea.Top)
                     {
                         top = tryTop;
                         // left 保持不变
                     }
                }

                // 策略 4: 如果右下都溢出 -> 尝试 Target 左上
                // (Target.Left - windowWidth - 10, Target.Top - windowHeight - 10)
                if (overflowRight && overflowBottom)
                {
                    left = target.Left - windowWidth - 10;
                    top = target.Top - windowHeight - 10;
                }
                
                // 最终兜底：强制限制在屏幕工作区内
                left = System.Math.Max(workArea.Left, System.Math.Min(left, workArea.Right - windowWidth));
                top = System.Math.Max(workArea.Top, System.Math.Min(top, workArea.Bottom - windowHeight));
                
                this.Left = left;
                this.Top = top;
            }
            else
            {
                double left = anchorX + 10;
                double top = anchorY + 10;

                // 智能避让逻辑
                bool overflowRight = (left + windowWidth > workArea.Right);
                bool overflowBottom = (top + windowHeight > workArea.Bottom);

                // 旧逻辑：仅基于鼠标点的简单避让
                if (overflowRight) left = mouseX - windowWidth - 15;
                if (overflowBottom) top = mouseY - windowHeight - 15;
                
                // 最终兜底：强制限制在屏幕工作区内
                left = System.Math.Max(workArea.Left, System.Math.Min(left, workArea.Right - windowWidth));
                top = System.Math.Max(workArea.Top, System.Math.Min(top, workArea.Bottom - windowHeight));
                
                this.Left = left;
                this.Top = top;
            }
        }

        public void UpdateText(string text)
        {
            ResultBox.Text = text;
        }

    }
}