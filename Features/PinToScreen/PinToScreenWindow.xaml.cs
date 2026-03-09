using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.PinToScreen
{
    public partial class PinToScreenWindow : Window
    {
        private static readonly object _syncLock = new object();
        private static readonly List<PinToScreenWindow> _windows = new List<PinToScreenWindow>();

        public BitmapSource? Image { get; private set; }
        public PinToScreenOptions Options { get; private set; }
        public new System.Windows.Media.Brush BorderBrush { get; private set; }
        public new Thickness BorderThickness { get; private set; }

        private int _imageOpacity = 100;
        private bool _isMinimized = false;

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

        public bool IsMinimized
        {
            get => _isMinimized;
            private set
            {
                _isMinimized = value;
                UpdateImageSize();
            }
        }

        private PinToScreenWindow(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null)
        {
            InitializeComponent();

            Options = options ?? new PinToScreenOptions();
            Image = image;

            _imageOpacity = Options.InitialOpacity;
            Topmost = Options.TopMost;
            Opacity = _imageOpacity / 100.0;

            BorderBrush = new SolidColorBrush(Options.BorderColor);
            BorderThickness = new Thickness(Options.BorderSize);
            ImageBorder.BorderBrush = BorderBrush;
            ImageBorder.BorderThickness = Options.Border ? BorderThickness : new Thickness(0);

            if (!Options.Shadow)
            {
                ShadowEffect.BlurRadius = 0;
                ShadowEffect.Opacity = 0;
            }

            PinnedImage.Source = Image;
            UpdateImageSize();

            if (location.HasValue)
            {
                Left = location.Value.X;
                Top = location.Value.Y;
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - Height - 20;
            }

            Focusable = true;
            GotFocus += (s, e) => Keyboard.Focus(this);

            Loaded += (s, e) =>
            {
                try
                {
                    var ps = PresentationSource.FromVisual(this);
                    double winDpi = ps?.CompositionTarget?.TransformToDevice.M11 ?? -1;
                    LoggerService.Instance.LogInfo(
                        $"[PIN-DIAG] Window Loaded: ActualWidth={ActualWidth:F1}, ActualHeight={ActualHeight:F1}, " +
                        $"PinnedImage.ActualWidth={PinnedImage.ActualWidth:F1}, PinnedImage.ActualHeight={PinnedImage.ActualHeight:F1}, " +
                        $"WindowDPI={winDpi:F3}",
                        "PinToScreenWindow");
                }
                catch { }
            };
        }

        public static void PinToScreenAsync(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null)
        {
            if (image == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
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
            }));
        }

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

            double scale = 1.0;
            double imgWidth, imgHeight;

            if (IsMinimized)
            {
                imgWidth = Options.MinimizeSize.Width;
                imgHeight = Options.MinimizeSize.Height;
            }
            else
            {
                double dpiScaleX = (Image.DpiX > 0 ? Image.DpiX : 96.0) / 96.0;
                double dpiScaleY = (Image.DpiY > 0 ? Image.DpiY : 96.0) / 96.0;
                imgWidth = (Image.PixelWidth / dpiScaleX) * scale;
                imgHeight = (Image.PixelHeight / dpiScaleY) * scale;
            }

            PinnedImage.Width = imgWidth;
            PinnedImage.Height = imgHeight;

            double winWidth = imgWidth;
            double winHeight = imgHeight;

            if (Options.Border)
            {
                winWidth += Options.BorderSize * 2;
                winHeight += Options.BorderSize * 2;
            }

            Width = winWidth;
            Height = winHeight;

            try
            {
                LoggerService.Instance.LogInfo(
                    $"[PIN-DIAG] UpdateImageSize: Image.PixelWidth={Image.PixelWidth}, Image.PixelHeight={Image.PixelHeight}, " +
                    $"Image.DpiX={Image.DpiX}, Image.DpiY={Image.DpiY}, dpiScale={((Image.DpiX > 0 ? Image.DpiX : 96.0) / 96.0):F3}, " +
                    $"scale={scale}, PinnedImage=[{imgWidth:F1}x{imgHeight:F1}], Window=[{winWidth:F1}x{winHeight:F1}]",
                    "PinToScreenWindow");
            }
            catch { }

            RenderOptions.SetBitmapScalingMode(PinnedImage, BitmapScalingMode.NearestNeighbor);
        }

        private void ResetImage()
        {
            ImageOpacity = 100;
        }

        private void ToggleMinimize()
        {
            IsMinimized = !IsMinimized;

            if (IsMinimized && ImageOpacity < 100)
            {
                Opacity = 1.0;
            }
            else if (!IsMinimized)
            {
                Opacity = ImageOpacity / 100.0;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount > 1)
                {
                    ToggleMinimize();
                }
                else if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { this.DragMove(); } catch { }
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                Close();
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                if (!IsMinimized) ResetImage();
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsMinimized) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    ImageOpacity += Options.OpacityStep;
                else
                    ImageOpacity -= Options.OpacityStep;
                e.Handled = true;
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            int speed = Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1;

            switch (e.Key)
            {
                case Key.Left: Left -= speed; e.Handled = true; break;
                case Key.Right: Left += speed; e.Handled = true; break;
                case Key.Up: Top -= speed; e.Handled = true; break;
                case Key.Down: Top += speed; e.Handled = true; break;
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!IsMinimized)
            {
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
