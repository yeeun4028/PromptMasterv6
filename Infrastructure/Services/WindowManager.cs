using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Views;
using System.Windows;
using Application = System.Windows.Application;
using PromptMasterv5.Infrastructure.Helpers;
using System;
using System.Threading.Tasks;

namespace PromptMasterv5.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        public byte[]? ShowCaptureWindow(Func<byte[], Task>? onCaptureProcessing = null)
        {
            // 1. Capture screen cleanly (before showing any overlay)
            // Note: Use a helper that uses System.Drawing so we don't block UI thread logic excessively
            using var screenBmp = Helpers.ScreenCaptureHelper.CaptureFullScreen();

            // 2. Show Overlay on UI thread
            if (Application.Current.Dispatcher.CheckAccess())
            {
                var capture = new ScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                if (capture.ShowDialog() == true)
                {
                    return capture.CapturedImageBytes;
                }
                return null;
            }
            else
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    // Clone bitmap because the original might lead to access issues across threads/contexts if not careful,
                    // but here we are just passing reference. However, Bitmap is not thread-safe.
                    // Since we created it in this method (which might be bg thread?), we should be careful.
                    // Wait, ShowCaptureWindow is likely called from ViewModel (BG thread?).
                    // ScreenCaptureHelper uses GDI+, which is fine on BG thread.
                    // We pass it to UI thread constructor.
                    // Bitmap needs to be used carefully.
                    
                    // Actually, GDI+ objects can be passed, but let's be safe.
                    var capture = new ScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                    if (capture.ShowDialog() == true)
                    {
                        return capture.CapturedImageBytes;
                    }
                    return null;
                });
            }
        }

        public void ShowTranslationPopup(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = new TranslationPopup(text);
                popup.Show();
                popup.Activate();
            });
        }
    }
}
