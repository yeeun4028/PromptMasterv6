using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PromptMasterv5.Views
{
    public partial class TranslationPopup : Window
    {
        public bool IsClosing { get; private set; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        public TranslationPopup(string initialText)
        {
            InitializeComponent();
            
            ResultBox.Text = initialText;
            
            this.Loaded += TranslationPopup_Loaded;
            
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
            var mousePhysical = System.Windows.Forms.Cursor.Position;
            
            double dpiX = 1.0;
            double dpiY = 1.0;
            
            try
            {
                var monitor = MonitorFromPoint(new POINT { X = mousePhysical.X, Y = mousePhysical.Y }, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var hr = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint monitorDpiX, out uint monitorDpiY);
                    if (hr >= 0 && monitorDpiX > 0)
                    {
                        dpiX = monitorDpiX / 96.0;
                        dpiY = monitorDpiY / 96.0;
                    }
                }
            }
            catch
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
            }

            double mouseX = mousePhysical.X / dpiX;
            double mouseY = mousePhysical.Y / dpiY;
            
            var workArea = SystemParameters.WorkArea;
            
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;
            
            if (windowWidth == 0 || windowHeight == 0)
            {
                this.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                windowWidth = this.DesiredSize.Width;
                windowHeight = this.DesiredSize.Height;
            }
            
            double anchorX = mouseX;
            double anchorY = mouseY;
            
            if (_placementTarget.HasValue)
            {
                var target = _placementTarget.Value;
                target.X += SystemParameters.VirtualScreenLeft;
                target.Y += SystemParameters.VirtualScreenTop;
                
                anchorX = target.Right;
                anchorY = target.Bottom;

                double left = anchorX + 10;
                double top = anchorY + 10;

                bool overflowRight = (left + windowWidth > workArea.Right);
                bool overflowBottom = (top + windowHeight > workArea.Bottom);

                if (overflowRight && !overflowBottom)
                {
                    double tryLeft = target.Left - windowWidth - 10;
                    if (tryLeft >= workArea.Left)
                    {
                         left = tryLeft;
                    }
                }

                if (!overflowRight && overflowBottom)
                {
                     double tryTop = target.Top - windowHeight - 10;
                     if (tryTop >= workArea.Top)
                     {
                         top = tryTop;
                     }
                }

                if (overflowRight && overflowBottom)
                {
                    left = target.Left - windowWidth - 10;
                    top = target.Top - windowHeight - 10;
                }
                
                left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - windowWidth));
                top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - windowHeight));
                
                this.Left = left;
                this.Top = top;
            }
            else
            {
                double left = anchorX + 10;
                double top = anchorY + 10;

                bool overflowRight = (left + windowWidth > workArea.Right);
                bool overflowBottom = (top + windowHeight > workArea.Bottom);

                if (overflowRight) left = mouseX - windowWidth - 15;
                if (overflowBottom) top = mouseY - windowHeight - 15;
                
                left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - windowWidth));
                top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - windowHeight));
                
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
