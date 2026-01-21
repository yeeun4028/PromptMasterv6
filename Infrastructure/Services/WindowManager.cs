using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Views;
using System.Windows;
using Application = System.Windows.Application;

namespace PromptMasterv5.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        public byte[]? ShowCaptureWindow()
        {
            // Must be run on UI thread
            if (Application.Current.Dispatcher.CheckAccess())
            {
                var capture = new ScreenCaptureOverlay();
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
                    var capture = new ScreenCaptureOverlay();
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
