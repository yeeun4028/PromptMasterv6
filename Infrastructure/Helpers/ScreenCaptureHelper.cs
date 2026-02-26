using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Forms;
using PromptMasterv5.Infrastructure.Services;

namespace PromptMasterv5.Infrastructure.Helpers
{
    public static class ScreenCaptureHelper
    {
        public static Bitmap CaptureFullScreen()
        {
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Bounds.X < minX) minX = screen.Bounds.X;
                if (screen.Bounds.Y < minY) minY = screen.Bounds.Y;
                if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
            }
            
            int width = maxX - minX;
            int height = maxY - minY;

            // 诊断日志：记录 Screen.AllScreens 返回的像素尺寸 vs WPF 逻辑尺寸
            LoggerService.Instance.LogInfo(
                $"[PIN-DIAG] CaptureFullScreen: Screen.AllScreens bounds=[{minX},{minY} {width}x{height}], " +
                $"SystemParameters.VirtualScreen=[{SystemParameters.VirtualScreenLeft},{SystemParameters.VirtualScreenTop} " +
                $"{SystemParameters.VirtualScreenWidth}x{SystemParameters.VirtualScreenHeight}]",
                "ScreenCaptureHelper");

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(minX, minY, 0, 0, new System.Drawing.Size(width, height));
            }

            LoggerService.Instance.LogInfo(
                $"[PIN-DIAG] CaptureFullScreen: bitmap created = {bmp.Width}x{bmp.Height}, " +
                $"bitmap.HorizontalResolution={bmp.HorizontalResolution}, bitmap.VerticalResolution={bmp.VerticalResolution}",
                "ScreenCaptureHelper");

            return bmp;
        }
    }
}
