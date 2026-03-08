using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Views
{
    public partial class ScreenCaptureOverlay : Window
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        private System.Windows.Point _startPoint;
        private bool _isSelecting;
        private bool _isProcessing;
        private Bitmap? _screenBitmap;

        private readonly Func<byte[], System.Windows.Rect, Task>? _processingCallback;
        
        public byte[]? CapturedImageBytes { get; private set; }

        public System.Windows.Rect CapturedRect { get; private set; }

        public ScreenCaptureOverlay(Bitmap? capturedScreen = null, Func<byte[], System.Windows.Rect, Task>? processingCallback = null)
        {
            InitializeComponent();
            _processingCallback = processingCallback;
            
            if (capturedScreen != null)
            {
                _screenBitmap = capturedScreen;
                SetBackgroundFromBitmap(_screenBitmap);
            }

            Loaded += ScreenCaptureOverlay_Loaded;
            PreviewKeyDown += (s, e) =>
            {
                if (_isProcessing) return;
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }

        private void ScreenCaptureOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            var diagSource = PresentationSource.FromVisual(this);
            double diagDpiX = diagSource?.CompositionTarget?.TransformToDevice.M11 ?? -1;
            LoggerService.Instance.LogInfo(
                $"[PIN-DIAG] Overlay Loaded: Window=[{this.Width}x{this.Height}], " +
                $"_screenBitmap=[{_screenBitmap?.Width}x{_screenBitmap?.Height}], " +
                $"DPI Scale from PresentationSource={diagDpiX}",
                "ScreenCaptureOverlay");

            this.Activate();
            this.Focus();
            
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
        }

        private void SetBackgroundFromBitmap(Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            try
            {
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                this.Background = new ImageBrush(bitmapSource)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            _isSelecting = true;
            
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;

            SelectionCanvas.CaptureMouse();
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isSelecting) return;

            var currentPoint = e.GetPosition(SelectionCanvas);

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        private async void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            _isSelecting = false;
            
            SelectionCanvas.ReleaseMouseCapture();
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;

            double x = Canvas.GetLeft(SelectionRect);
            double y = Canvas.GetTop(SelectionRect);
            double width = SelectionRect.Width;
            double height = SelectionRect.Height;

            if (width < 10 || height < 10)
            {
                DialogResult = false;
                Close();
                return;
            }

            CaptureSelectedRegion((int)x, (int)y, (int)width, (int)height);
            CapturedRect = new System.Windows.Rect(x, y, width, height);
            
            if (_processingCallback != null && CapturedImageBytes != null)
            {
                EnterProcessingState();
                
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                double dpiY = 1.0;
                if (source?.CompositionTarget != null) 
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                var selectionRect = new Rect(x, y, width, height);

                try
                {
                    await _processingCallback(CapturedImageBytes, selectionRect).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogError($"Processing failed: {ex.Message}", "ScreenCaptureOverlay");
                }
            }

            DialogResult = true;
            Close();
        }

        private void EnterProcessingState()
        {
            _isProcessing = true;
            SelectionCanvas.IsHitTestVisible = false;
            
            double rectX = Canvas.GetLeft(SelectionRect);
            double rectY = Canvas.GetTop(SelectionRect);
            double rectW = SelectionRect.Width;
            double rectH = SelectionRect.Height;

            SelectionRect.Visibility = Visibility.Collapsed;
            
            Canvas.SetLeft(LoadingSpinner, rectX + rectW - 8);
            Canvas.SetTop(LoadingSpinner, rectY + rectH - 8);
            LoadingSpinner.Visibility = Visibility.Visible;

            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void CaptureSelectedRegion(int x, int y, int width, int height)
        {
            try
            {
                if (_screenBitmap == null)
                {
                    CapturedImageBytes = null;
                    return;
                }

                var logicalTopLeft = new System.Windows.Point(x, y);
                var logicalBottomRight = new System.Windows.Point(x + width, y + height);

                var physTopLeft = this.PointToScreen(logicalTopLeft);
                var physBottomRight = this.PointToScreen(logicalBottomRight);

                int physX = (int)physTopLeft.X - System.Windows.Forms.SystemInformation.VirtualScreen.Left;
                int physY = (int)physTopLeft.Y - System.Windows.Forms.SystemInformation.VirtualScreen.Top;
                int physWidth = (int)(physBottomRight.X - physTopLeft.X);
                int physHeight = (int)(physBottomRight.Y - physTopLeft.Y);

                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] CaptureSelectedRegion: _screenBitmap=[{_screenBitmap.Width}x{_screenBitmap.Height}], " +
                    $"Logical selection=[{x},{y} {width}x{height}], " +
                    $"Physical crop=[{physX},{physY} {physWidth}x{physHeight}]",
                    "ScreenCaptureOverlay");

                physX = Math.Max(0, physX);
                physY = Math.Max(0, physY);
                if (physX + physWidth > _screenBitmap.Width) physWidth = _screenBitmap.Width - physX;
                if (physY + physHeight > _screenBitmap.Height) physHeight = _screenBitmap.Height - physY;

                if (physWidth <= 0 || physHeight <= 0)
                {
                    LoggerService.Instance.LogError($"Invalid Capture Dimensions after coordinate transform: {physWidth}x{physHeight}", "ScreenCaptureOverlay");
                    CapturedImageBytes = null;
                    return;
                }

                using var croppedBmp = new Bitmap(physWidth, physHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(croppedBmp))
                {
                    g.DrawImage(_screenBitmap, 
                        new Rectangle(0, 0, physWidth, physHeight),
                        new Rectangle(physX, physY, physWidth, physHeight),
                        GraphicsUnit.Pixel);
                }

                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] CaptureSelectedRegion: croppedBmp=[{croppedBmp.Width}x{croppedBmp.Height}]",
                    "ScreenCaptureOverlay");

                using var stream = new MemoryStream();
                croppedBmp.Save(stream, ImageFormat.Png);
                CapturedImageBytes = stream.ToArray();

                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] CaptureSelectedRegion: PNG bytes={CapturedImageBytes.Length}",
                    "ScreenCaptureOverlay");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Region capture error: {ex.Message}");
                CapturedImageBytes = null;
            }
            finally
            {
                _screenBitmap?.Dispose();
                _screenBitmap = null;
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isProcessing) return;
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            Mouse.OverrideCursor = null;
            _screenBitmap?.Dispose();
            base.OnClosed(e);
        }
    }
}
