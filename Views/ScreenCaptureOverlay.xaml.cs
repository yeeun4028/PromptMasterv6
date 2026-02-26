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
using PromptMasterv5.Infrastructure.Services;

namespace PromptMasterv5.Views
{
    public partial class ScreenCaptureOverlay : Window
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        private System.Windows.Point _startPoint;
        private bool _isSelecting;
        private Bitmap? _screenBitmap;

        private readonly Func<byte[], System.Windows.Rect, Task>? _processingCallback;
        
        public byte[]? CapturedImageBytes { get; private set; }

        public ScreenCaptureOverlay(Bitmap? capturedScreen = null, Func<byte[], System.Windows.Rect, Task>? processingCallback = null)
        {
            InitializeComponent();
            _processingCallback = processingCallback;
            
            if (capturedScreen != null)
            {
                // 直接使用传入位图，不克隆，此对象拥有所有权并负责释放
                _screenBitmap = capturedScreen;
                
                // Set the Window Background to the captured static screen
                SetBackgroundFromBitmap(_screenBitmap);
            }

            Loaded += ScreenCaptureOverlay_Loaded;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }

        private void ScreenCaptureOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Span the entire virtual screen (all monitors) in logical WPF units
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            // 诊断日志：覆盖层窗口 vs 截图位图
            var diagSource = PresentationSource.FromVisual(this);
            double diagDpiX = diagSource?.CompositionTarget?.TransformToDevice.M11 ?? -1;
            LoggerService.Instance.LogInfo(
                $"[PIN-DIAG] Overlay Loaded: Window=[{this.Width}x{this.Height}], " +
                $"_screenBitmap=[{_screenBitmap?.Width}x{_screenBitmap?.Height}], " +
                $"DPI Scale from PresentationSource={diagDpiX}",
                "ScreenCaptureOverlay");

            // Ensure we are active and focused
            this.Activate();
            this.Focus();
            
            // Hide cursor for custom crosshair
            Mouse.OverrideCursor = System.Windows.Input.Cursors.None;

            if (_screenBitmap == null)
            {
                // Fallback if no bitmap passed (shouldn't happen with new logic, but safe)
                 CaptureFullScreenFallback();
            }
        }

        private void CaptureFullScreenFallback()
        {
            try
            {
                // Get physical screen bounds
                int minX = 0, minY = 0, maxX = 0, maxY = 0;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.Bounds.X < minX) minX = screen.Bounds.X;
                    if (screen.Bounds.Y < minY) minY = screen.Bounds.Y;
                    if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                    if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
                }
                
                int width = maxX - minX;
                int height = maxY - minY;

                _screenBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(_screenBitmap))
                {
                    g.CopyFromScreen(minX, minY, 0, 0, new System.Drawing.Size(width, height));
                }
                
                SetBackgroundFromBitmap(_screenBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screen capture error: {ex.Message}");
            }
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
                    Stretch = Stretch.Fill, // Use Fill to map the physical image boundaries exactly to the logical window boundaries
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

            // Hide guides when selection starts - they will not reappear for this session
            HorizontalGuide.Visibility = Visibility.Collapsed;
            VerticalGuide.Visibility = Visibility.Collapsed;

            SelectionCanvas.CaptureMouse();
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var currentPoint = e.GetPosition(SelectionCanvas);

            // Update Full-screen Crosshair only if visible
            if (HorizontalGuide.Visibility == Visibility.Visible)
            {
                HorizontalGuide.X1 = 0;
                HorizontalGuide.X2 = SelectionCanvas.ActualWidth;
                HorizontalGuide.Y1 = currentPoint.Y;
                HorizontalGuide.Y2 = currentPoint.Y;

                VerticalGuide.X1 = currentPoint.X;
                VerticalGuide.X2 = currentPoint.X;
                VerticalGuide.Y1 = 0;
                VerticalGuide.Y2 = SelectionCanvas.ActualHeight;
            }

            if (!_isSelecting) return;


            
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

            // Get final selection bounds
            double x = Canvas.GetLeft(SelectionRect);
            double y = Canvas.GetTop(SelectionRect);
            double width = SelectionRect.Width;
            double height = SelectionRect.Height;

            // Minimum selection size
            if (width < 10 || height < 10)
            {
                DialogResult = false;
                Close();
                return;
            }

            // Capture the selected region
            CaptureSelectedRegion((int)x, (int)y, (int)width, (int)height);
            
            if (_processingCallback != null && CapturedImageBytes != null)
            {
                EnterProcessingState();
                
                // Calculate Spinner Position (logic for UI remains same)
                // But we pass the logical Rect to ViewModel
                // Get DPI scale factor
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                double dpiY = 1.0;
                if (source?.CompositionTarget != null) 
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                // x, y, width, height are already Lognical because they come from Canvas.GetLeft (WPF coordinates)
                var selectionRect = new Rect(x, y, width, height);

                // We use Dispatcher to ensure UI update logic happens before we await (EnterProcessingState is sync)
                // But we act async here.
                await _processingCallback(CapturedImageBytes, selectionRect);
            }

            DialogResult = true;
            Close();
        }

        private void EnterProcessingState()
        {
            // Calculate final selection bounds before hiding
            double rectX = Canvas.GetLeft(SelectionRect);
            double rectY = Canvas.GetTop(SelectionRect);
            double rectW = SelectionRect.Width;
            double rectH = SelectionRect.Height;

            // Hide UI elements to show "Processing" state (clean feedback)
            SelectionRect.Visibility = Visibility.Collapsed;
            HorizontalGuide.Visibility = Visibility.Collapsed;
            VerticalGuide.Visibility = Visibility.Collapsed;
            // Cursor remains None as per requirement
            
            // Show Loading Spinner at bottom-right of selection
            // Align center of spinner (16x16) to the corner
            Canvas.SetLeft(LoadingSpinner, rectX + rectW - 8);
            Canvas.SetTop(LoadingSpinner, rectY + rectH - 8);
            LoadingSpinner.Visibility = Visibility.Visible;

            // Force redraw/update
            // 通知 WPF 渲染线程刷新 UI，避免使用 DoEvents() 可能引发的重入问题
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void CaptureSelectedRegion(int x, int y, int width, int height)
        {
            try
            {
                if (_screenBitmap == null)
                {
                    // Fallback: capture directly
                    int screenLeft = (int)SystemParameters.VirtualScreenLeft;
                    int screenTop = (int)SystemParameters.VirtualScreenTop;

                    using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(screenLeft + x, screenTop + y, 0, 0, new System.Drawing.Size(width, height));
                    }
                    
                    using var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);
                    CapturedImageBytes = ms.ToArray();
                    return;
                }
                // Get DPI scale factor
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                double dpiY = 1.0;
                if (source?.CompositionTarget != null) 
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                int physX = (int)(x * dpiX);
                int physY = (int)(y * dpiY);
                int physWidth = (int)(width * dpiX);
                int physHeight = (int)(height * dpiY);

                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] CaptureSelectedRegion: _screenBitmap=[{_screenBitmap.Width}x{_screenBitmap.Height}], " +
                    $"Logical selection=[{x},{y} {width}x{height}], DPI=[{dpiX}x{dpiY}], " +
                    $"Physical crop=[{physX},{physY} {physWidth}x{physHeight}]",
                    "ScreenCaptureOverlay");

                // Ensure bounds
                physX = Math.Max(0, physX);
                physY = Math.Max(0, physY);
                if (physX + physWidth > _screenBitmap.Width) physWidth = _screenBitmap.Width - physX;
                if (physY + physHeight > _screenBitmap.Height) physHeight = _screenBitmap.Height - physY;

                if (physWidth <= 0 || physHeight <= 0)
                {
                    LoggerService.Instance.LogError($"Invalid Capture Dimensions after DPI scaling: {physWidth}x{physHeight}", "ScreenCaptureOverlay");
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

                // 将屏幕实际 DPI 写入位图元数据
                croppedBmp.SetResolution((float)(96 * dpiX), (float)(96 * dpiY));

                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] CaptureSelectedRegion: croppedBmp=[{croppedBmp.Width}x{croppedBmp.Height}], " +
                    $"Resolution=[{croppedBmp.HorizontalResolution}x{croppedBmp.VerticalResolution}]",
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
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            Mouse.OverrideCursor = null; // Reset cursor
            _screenBitmap?.Dispose();
            base.OnClosed(e);
        }
    }
}
