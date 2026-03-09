using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Infrastructure.Helpers
{
    public static class TrayMenuHelper
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static void ShowContextMenu(ContextMenu menu)
        {
            if (menu == null) return;

            if (GetCursorPos(out POINT point))
            {
                // DPI Scaling factor detection
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;

                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var source = PresentationSource.FromVisual(mainWindow);
                    if (source != null && source.CompositionTarget != null)
                    {
                        dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                        dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                    }
                }

                // Set the placement target to something active
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                
                // Convert Physical Pixels to DIPs (Device Independent Pixels)
                menu.HorizontalOffset = point.X / dpiScaleX;
                menu.VerticalOffset = point.Y / dpiScaleY;
                
                // Set IsOpen to true to show it
                menu.IsOpen = true;

                // Win32 magic to ensure it closes when clicking outside
                if (mainWindow != null)
                {
                    IntPtr handle = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
                    NativeMethods.SetForegroundWindow(handle);
                }
            }
        }
    }
}
