using System;
using System.Windows;
using System.Windows.Media.Imaging;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IWindowRegistry
    {
        void RegisterWindow<TViewModel, TWindow>() where TWindow : Window;
        Window? CreateWindow(object viewModel);
        Window? CreateWindow<TViewModel>();
        Window? CreateWindow(string viewModelName);

        void RegisterScreenCaptureOverlay(Func<System.Drawing.Bitmap, Func<byte[], Rect, Task>?, Window> factory);
        Window CreateScreenCaptureOverlay(System.Drawing.Bitmap screenBitmap, Func<byte[], Rect, Task>? onCaptureProcessing);

        void RegisterTranslationPopup(Func<string, Window> factory);
        Window CreateTranslationPopup(string text);

        void RegisterPinToScreen(Action<BitmapSource, PinToScreenOptions?, System.Windows.Point?> pinAction);
        void PinToScreen(BitmapSource image, PinToScreenOptions? options, System.Windows.Point? location);

        void RegisterPinToScreenCloseAll(Action closeAllAction);
        void CloseAllPinToScreenWindows();

        void RegisterPinToScreenCountProvider(Func<int> countProvider);
        int GetPinToScreenWindowCount();
    }
}
