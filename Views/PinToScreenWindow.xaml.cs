using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;

using WpfBrush = System.Windows.Media.Brush;
using WpfThickness = System.Windows.Thickness;
using WpfPoint = System.Windows.Point;
using WpfCursors = System.Windows.Input.Cursors;
using WpfCursor = System.Windows.Input.Cursor;
using WpfClipboard = System.Windows.Clipboard;

namespace PromptMasterv5.Views
{
    public partial class PinToScreenWindow : Window
    {
        private static readonly object _syncLock = new object();
        private static readonly List<PinToScreenWindow> _windows = new List<PinToScreenWindow>();

        /// <summary>
        /// 当前显示的图片
        /// </summary>
        public BitmapSource? Image { get; private set; }

        /// <summary>
        /// 配置选项
        /// </summary>
        public PinToScreenOptions Options { get; private set; }

        /// <summary>
        /// 边框颜色
        /// </summary>
        public new WpfBrush BorderBrush { get; private set; }

        /// <summary>
        /// 边框宽度
        /// </summary>
        public new WpfThickness BorderThickness { get; private set; }

        private int _imageScale = 100;
        private int _imageOpacity = 100;
        private bool _isDragging = false;
        private WpfPoint _dragStartPoint;
        private bool _isMinimized = false;
        private DispatcherTimer? _toolbarTimer;

        /// <summary>
        /// 当前缩放比例 (20-500)
        /// <summary>
        /// 当前透明度 (10-100)
        /// </summary>
        public int ImageOpacity
        {
            get => _imageOpacity;
            set
            {
                int newOpacity = Math.Clamp(value, 10, 100);
                if (_imageOpacity != newOpacity)
                {
                    _imageOpacity = newOpacity;
                    Opacity = _imageOpacity / 100.0;
                }
            }
        }

        /// <summary>
        /// 是否处于最小化状态
        /// </summary>
        public bool IsMinimized
        {
            get => _isMinimized;
            private set
            {
                _isMinimized = value;
                UpdateImageSize();
            }
        }

        private PinToScreenWindow(BitmapSource image, PinToScreenOptions? options = null, WpfPoint? location = null)
        {
            InitializeComponent();

            Options = options ?? new PinToScreenOptions();
            Image = image;

            // 应用配置
            _imageOpacity = Options.InitialOpacity;
            Topmost = Options.TopMost;
            Opacity = _imageOpacity / 100.0;

            // 设置边框
            BorderBrush = new SolidColorBrush(Options.BorderColor);
            BorderThickness = new Thickness(Options.BorderSize);
            ImageBorder.BorderBrush = BorderBrush;
            ImageBorder.BorderThickness = Options.Border ? BorderThickness : new Thickness(0);

            // 设置阴影
            if (!Options.Shadow)
            {
                ShadowEffect.BlurRadius = 0;
                ShadowEffect.Opacity = 0;
            }

            // 设置图片
            PinnedImage.Source = Image;
            UpdateImageSize();

            // 设置位置
            if (location.HasValue)
            {
                Left = location.Value.X;
                Top = location.Value.Y;
            }
            else
            {
                // 默认显示在屏幕右下角
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - Height - 20;
            }

            // 初始化工具栏自动隐藏计时器
            _toolbarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _toolbarTimer.Tick += (s, e) =>
            {
                Toolbar.Visibility = Visibility.Collapsed;
                _toolbarTimer.Stop();
            };

            // 设置焦点以接收键盘输入
            Focusable = true;
            GotFocus += (s, e) => Keyboard.Focus(this);

            // 诊断：在 WPF 布局完成后记录实际渲染尺寸
            Loaded += (s, e) =>
            {
                try
                {
                    var ps = PresentationSource.FromVisual(this);
                    double winDpi = ps?.CompositionTarget?.TransformToDevice.M11 ?? -1;
                    Infrastructure.Services.LoggerService.Instance.LogInfo(
                        $"[PIN-DIAG] Window Loaded: ActualWidth={ActualWidth:F1}, ActualHeight={ActualHeight:F1}, " +
                        $"PinnedImage.ActualWidth={PinnedImage.ActualWidth:F1}, PinnedImage.ActualHeight={PinnedImage.ActualHeight:F1}, " +
                        $"WindowDPI={winDpi:F3}",
                        "PinToScreenWindow");
                }
                catch { }
            };
        }

        /// <summary>
        /// 异步创建并显示贴图窗口（在独立线程中运行）
        /// </summary>
        public static void PinToScreenAsync(BitmapSource image, PinToScreenOptions? options = null, WpfPoint? location = null)
        {
            if (image == null) return;

            var thread = new Thread(() =>
            {
                var window = new PinToScreenWindow(image, options, location);

                lock (_syncLock)
                {
                    _windows.Add(window);
                }

                window.Closed += (s, e) =>
                {
                    lock (_syncLock)
                    {
                        _windows.Remove(window);
                    }
                };

                window.Show();
                window.Activate();

                // 启动此线程的 Dispatcher
                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 关闭所有贴图窗口
        /// </summary>
        public static void CloseAll()
        {
            List<PinToScreenWindow> windowsToClose;
            lock (_syncLock)
            {
                windowsToClose = new List<PinToScreenWindow>(_windows);
            }

            foreach (var window in windowsToClose)
            {
                window.Dispatcher.Invoke(() => window.Close());
            }
        }

        /// <summary>
        /// 获取当前打开的贴图窗口数量
        /// </summary>
        public static int OpenWindowCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _windows.Count;
                }
            }
        }

        private void UpdateImageSize()
        {
            if (Image == null) return;

            double scale = 1.0; // 移除缩放功能，固定为100%
            double imgWidth, imgHeight;

            if (IsMinimized)
            {
                imgWidth = Options.MinimizeSize.Width;
                imgHeight = Options.MinimizeSize.Height;
            }
            else
            {
                // 将物理像素转换为 WPF 逻辑尺寸（设备无关单位）。
                double dpiScaleX = (Image.DpiX > 0 ? Image.DpiX : 96.0) / 96.0;
                double dpiScaleY = (Image.DpiY > 0 ? Image.DpiY : 96.0) / 96.0;
                imgWidth = (Image.PixelWidth / dpiScaleX) * scale;
                imgHeight = (Image.PixelHeight / dpiScaleY) * scale;
            }

            // 显式设置 Image 控件的尺寸，防止其按自然像素大小显示
            PinnedImage.Width = imgWidth;
            PinnedImage.Height = imgHeight;

            double winWidth = imgWidth;
            double winHeight = imgHeight;

            // 添加边框
            if (Options.Border)
            {
                winWidth += Options.BorderSize * 2;
                winHeight += Options.BorderSize * 2;
            }

            Width = winWidth;
            Height = winHeight;

            // 诊断日志
            try
            {
                Infrastructure.Services.LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] UpdateImageSize: Image.PixelWidth={Image.PixelWidth}, Image.PixelHeight={Image.PixelHeight}, " +
                    $"Image.DpiX={Image.DpiX}, Image.DpiY={Image.DpiY}, dpiScale={((Image.DpiX > 0 ? Image.DpiX : 96.0) / 96.0):F3}, " +
                    $"scale={scale}, PinnedImage=[{imgWidth:F1}x{imgHeight:F1}], Window=[{winWidth:F1}x{winHeight:F1}]",
                    "PinToScreenWindow");
            }
            catch { }

            // 移除根据缩放百分比调整缩放模式的逻辑，因为目前已经固定缩放比例为 100%
            RenderOptions.SetBitmapScalingMode(PinnedImage, BitmapScalingMode.NearestNeighbor);
        }

        private void ShowToolbar()
        {
            Toolbar.Visibility = Visibility.Visible;
            _toolbarTimer?.Stop();
            _toolbarTimer?.Start();
        }

        private void HideToolbar()
        {
            _toolbarTimer?.Stop();
            Toolbar.Visibility = Visibility.Collapsed;
        }

        private void ResetImage()
        {
            ImageOpacity = 100;
        }

        private void ToggleMinimize()
        {
            IsMinimized = !IsMinimized;

            // 最小化时临时设置不透明
            if (IsMinimized && ImageOpacity < 100)
            {
                Opacity = 1.0;
            }
            else if (!IsMinimized)
            {
                Opacity = ImageOpacity / 100.0;
            }
        }

        #region 鼠标事件

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount > 1)
                {
                    // 双击切换最小化
                    ToggleMinimize();
                }
                else
                {
                    // 开始拖拽
                    _isDragging = true;
                    _dragStartPoint = e.GetPosition(this);
                    Mouse.Capture(this);
                    Cursor = WpfCursors.SizeAll;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                // 右键关闭
                Close();
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                // 中键重置
                if (!IsMinimized)
                {
                    ResetImage();
                }
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                WpfPoint currentPoint = e.GetPosition(this);
                double offsetX = currentPoint.X - _dragStartPoint.X;
                double offsetY = currentPoint.Y - _dragStartPoint.Y;

                Left += offsetX;
                Top += offsetY;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                Mouse.Capture(null);
                Cursor = WpfCursors.Arrow;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsMinimized) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl + 滚轮：调整透明度
                if (e.Delta > 0)
                    ImageOpacity += Options.OpacityStep;
                else
                    ImageOpacity -= Options.OpacityStep;
                e.Handled = true;
            }
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ShowToolbar();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                HideToolbar();
            }
        }

        #endregion

        #region 键盘事件

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            int speed = Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1;

            switch (e.Key)
            {
                case Key.Left:
                    Left -= speed;
                    e.Handled = true;
                    break;
                case Key.Right:
                    Left += speed;
                    e.Handled = true;
                    break;
                case Key.Up:
                    Top -= speed;
                    e.Handled = true;
                    break;
                case Key.Down:
                    Top += speed;
                    e.Handled = true;
                    break;
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 移除 Ctrl+C 复制图片功能

            if (!IsMinimized)
            {
                // +/- 调整透明度
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ImageOpacity += Options.OpacityStep;
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ImageOpacity -= Options.OpacityStep;
                        e.Handled = true;
                    }
                }
            }
        }

        #endregion

        #region 按钮事件

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _toolbarTimer?.Stop();
            base.OnClosed(e);
            // 关闭此窗口所在 STA 线程的 Dispatcher 消息循环，
            // 使 PinToScreenAsync 中 Dispatcher.Run() 返回，线程正常退出。
            // 没有这一行，每个贴图窗口关闭后线程会永久挂起，积累导致系统卡顿。
            Dispatcher.InvokeShutdown();
        }
    }
}
