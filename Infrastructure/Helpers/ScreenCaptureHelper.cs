using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PromptMasterv6.Infrastructure.Helpers
{
    public static class ScreenCaptureHelper
    {
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        public static Bitmap? CaptureFullScreen()
        {
            IntPtr originalContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            try
            {
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;

                foreach (Screen screen in Screen.AllScreens)
                {
                    minX = Math.Min(minX, screen.Bounds.X);
                    minY = Math.Min(minY, screen.Bounds.Y);
                    maxX = Math.Max(maxX, screen.Bounds.Right);
                    maxY = Math.Max(maxY, screen.Bounds.Bottom);
                }

                int width = maxX - minX;
                int height = maxY - minY;

                if (width <= 0 || height <= 0) return null;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(minX, minY, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                return bmp;
            }
            finally
            {
                SetThreadDpiAwarenessContext(originalContext);
            }
        }
    }
}
