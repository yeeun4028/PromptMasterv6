using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.PinToScreen
{
    public partial class PinToScreenWindow : Window
    {
        private readonly LoggerService _logger;

        public BitmapSource? Image { get; private set; }
        public PinToScreenOptions Options { get; private set; }
        public new System.Windows.Media.Brush BorderBrush { get; private set; }
        public new Thickness BorderThickness { get; private set; }

        private PinToScreenWindow(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null, LoggerService? logger = null)
        {
            InitializeComponent();
            _logger = logger ?? LoggerService.Instance;

            Options = options ?? new PinToScreenOptions();
            Image = image;

            Topmost = Options.TopMost;
            Opacity = Options.InitialOpacity / 100.0;

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
        }

        public static void PinToScreenAsync(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null)
        {
            if (image == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = new PinToScreenWindow(image, options, location);
                window.Show();
                window.Activate();
            }));
        }

        private void UpdateImageSize()
        {
            if (Image == null) return;

            double dpiScaleX = (Image.DpiX > 0 ? Image.DpiX : 96.0) / 96.0;
            double dpiScaleY = (Image.DpiY > 0 ? Image.DpiY : 96.0) / 96.0;
            double imgWidth = Image.PixelWidth / dpiScaleX;
            double imgHeight = Image.PixelHeight / dpiScaleY;

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

            RenderOptions.SetBitmapScalingMode(PinnedImage, BitmapScalingMode.NearestNeighbor);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { this.DragMove(); } catch { }
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                Close();
            }
        }
    }
}
