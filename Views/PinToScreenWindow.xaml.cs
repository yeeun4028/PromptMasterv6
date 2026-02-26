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
        /// </summary>
        public int ImageScale
        {
            get => _imageScale;
            set
            {
                int newScale = Math.Clamp(value, 20, 500);
                if (_imageScale != newScale)
                {
                    _imageScale = newScale;
                    UpdateImageSize();
                    UpdateScaleText();
                }
            }
        }

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
            _imageScale = Options.InitialScale;
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
            UpdateScaleText();

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

            double scale = _imageScale / 100.0;
            double width, height;

            if (IsMinimized)
            {
                width = Options.MinimizeSize.Width;
                height = Options.MinimizeSize.Height;
            }
            else
            {
                width = Image.PixelWidth * scale;
                height = Image.PixelHeight * scale;
            }

            // 添加边框
            if (Options.Border)
            {
                width += Options.BorderSize * 2;
                height += Options.BorderSize * 2;
            }

            Width = width;
            Height = height;

            // 更新图片缩放模式
            if (_imageScale == 100 && !IsMinimized)
            {
                RenderOptions.SetBitmapScalingMode(PinnedImage, BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                RenderOptions.SetBitmapScalingMode(PinnedImage,
                    Options.HighQualityScale ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            }
        }

        private void UpdateScaleText()
        {
            ScaleText.Text = $"{_imageScale}%";
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
            ImageScale = 100;
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
            }
            else
            {
                // 滚轮：调整缩放
                if (e.Delta > 0)
                    ImageScale += Options.ScaleStep;
                else
                    ImageScale -= Options.ScaleStep;
            }

            e.Handled = true;
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
            // Ctrl+C 复制图片
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyImageToClipboard();
                e.Handled = true;
                return;
            }

            if (!IsMinimized)
            {
                // +/- 调整缩放
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ImageOpacity += Options.OpacityStep;
                    else
                        ImageScale += Options.ScaleStep;
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ImageOpacity -= Options.OpacityStep;
                    else
                        ImageScale -= Options.ScaleStep;
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region 按钮事件

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyImageToClipboard();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ScaleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsMinimized)
            {
                ImageScale = 100;
            }
            e.Handled = true;
        }

        #endregion

        private void CopyImageToClipboard()
        {
            try
            {
                if (Image != null)
                {
                    WpfClipboard.SetImage(Image);
                    LoggerService.Instance.LogInfo("图片已复制到剪贴板", "PinToScreenWindow");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"复制图片失败: {ex.Message}", "PinToScreenWindow");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _toolbarTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
