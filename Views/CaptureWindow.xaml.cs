using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
// ★★★ 核心修复：显式指定输入事件为 WPF 类型，解决与 WinForms 的命名冲突 ★★★
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Key = System.Windows.Input.Key;
using MouseButton = System.Windows.Input.MouseButton;
// ★★★ 引用 System.Drawing 用于屏幕截图逻辑 ★★★
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
// ★★★ 解决 Point 和 Rectangle 的命名冲突 ★★★
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
namespace PromptMasterv5.Views
{
public partial class CaptureWindow : Window
{
private Point _startPoint;
private bool _isDragging;

// 结果：截图的字节数组
    public byte[]? CapturedImageBytes { get; private set; }

    public CaptureWindow()
    {
        InitializeComponent();

        // 覆盖全屏
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    private void CanvasArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            this.DialogResult = false;
            this.Close();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CanvasArea);

            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }
    }

    private void CanvasArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(CanvasArea);
        double x = Math.Min(_startPoint.X, current.X);
        double y = Math.Min(_startPoint.Y, current.Y);
        double w = Math.Abs(_startPoint.X - current.X);
        double h = Math.Abs(_startPoint.Y - current.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        UpdateMask(x, y, w, h);
    }

    private void UpdateMask(double x, double y, double w, double h)
    {
        // 使用 GeometryGroup 组合出一个中间镂空的遮罩
        var bgGeo = new RectangleGeometry(new Rect(0, 0, CanvasArea.ActualWidth, CanvasArea.ActualHeight));
        var holeGeo = new RectangleGeometry(new Rect(x, y, w, h));
        var group = new GeometryGroup();
        group.Children.Add(bgGeo);
        group.Children.Add(holeGeo);
        MaskPath.Data = group;
    }

    private void CanvasArea_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        double x = Canvas.GetLeft(SelectionRect);
        double y = Canvas.GetTop(SelectionRect);
        double w = SelectionRect.Width;
        double h = SelectionRect.Height;

        // 防止误触
        if (w < 5 || h < 5)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        this.Visibility = Visibility.Collapsed; // 隐藏窗口以便截图

        // 截取屏幕
        try
        {
            // 获取 DPI 缩放比例
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null && source.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            int physX = (int)(x * dpiX);
            int physY = (int)(y * dpiY);
            int physW = (int)(w * dpiX);
            int physH = (int)(h * dpiY);

            // 处理多屏偏移
            int screenLeft = (int)(Left * dpiX);
            int screenTop = (int)(Top * dpiY);

            // 避免创建尺寸为 0 的 Bitmap 导致异常
            if (physW <= 0) physW = 1;
            if (physH <= 0) physH = 1;

            using (var bmp = new Bitmap(physW, physH))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screenLeft + physX, screenTop + physY, 0, 0, new System.Drawing.Size(physW, physH));
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    CapturedImageBytes = ms.ToArray();
                }
            }

            this.DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"截图失败: {ex.Message}");
            this.DialogResult = false;
        }
        finally
        {
            this.Close();
        }
    }
}
}