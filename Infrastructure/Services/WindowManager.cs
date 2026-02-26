using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Views;
using PromptMasterv5.ViewModels;
using System.Windows;
using Application = System.Windows.Application;
using PromptMasterv5.Infrastructure.Helpers;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media.Imaging;
using PromptMasterv5.Core.Models;
using System.IO;

using WpfClipboard = System.Windows.Clipboard;

namespace PromptMasterv5.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        public async Task<byte[]?> ShowCaptureWindowAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null)
        {
            // 1. Capture screen instantly before any UI thread manipulation or window closing
            // This is the "Static Screen Freeze" approach. It prevents tooltips from hiding when focus changes.
            Bitmap? screenBmp = null;
            try 
            {
                screenBmp = Helpers.ScreenCaptureHelper.CaptureFullScreen();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to capture screen instantly: {ex.Message}", "WindowManager");
                return null;
            }

            // 2. Cleanup: Close any existing TranslationPopup windows AFTER capture
            bool anyClosed = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var existingPopups = new System.Collections.Generic.List<Window>();
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is TranslationPopup)
                    {
                        existingPopups.Add(win);
                    }
                }

                foreach (var popup in existingPopups)
                {
                    try
                    {
                        if (popup.IsLoaded)
                        {
                            popup.Close();
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore race conditions where window is already closing
                    }
                }
                
                return existingPopups.Count > 0;
            });

            // 3. Show Overlay on UI thread using the static background image
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainWin = Application.Current.MainWindow as MainWindow;
                if (mainWin != null) mainWin.SuppressAutoActivation = true;

                IntPtr previousHwnd = NativeMethods.GetForegroundWindow();

                // Temporarily hide LaunchBarWindow so it doesn't block the capture overlay.
                // Both windows have Topmost=True; without hiding, LaunchBarWindow (the 6px strip
                // on the left edge) sits on top of ScreenCaptureOverlay and swallows mouse events,
                // making the screenshot selection appear unresponsive.
                var launchBarWindows = new System.Collections.Generic.List<LaunchBarWindow>();
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is LaunchBarWindow lbw && lbw.IsVisible)
                    {
                        launchBarWindows.Add(lbw);
                        lbw.Hide();
                    }
                }

                var capture = new ScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                
                byte[]? result = null;
                try
                {
                    if (capture.ShowDialog() == true)
                    {
                        result = capture.CapturedImageBytes;
                    }
                }
                finally
                {
                    // Restore LaunchBarWindow visibility after capture dialog closes
                    foreach (var lbw in launchBarWindows)
                    {
                        lbw.Show();
                    }

                    if (result == null)
                    {
                        screenBmp?.Dispose();
                    }

                    if (previousHwnd != IntPtr.Zero)
                    {
                        var currentMainHwnd = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                        if (previousHwnd != currentMainHwnd)
                        {
                            NativeMethods.SetForegroundWindow(previousHwnd);
                        }
                    }

                    if (mainWin != null)
                    {
                        // Slight delay to ensure the OS activation event has passed
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                        {
                            mainWin.SuppressAutoActivation = false;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                return result;
            });
        }

        public void ShowTranslationPopup(string text, System.Windows.Rect? placementTarget = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = new TranslationPopup(text);
                if (placementTarget.HasValue)
                {
                    popup.SetPlacementTarget(placementTarget.Value);
                    
                    // Basic boundary check to ensure it doesn't spawn partially off-screen
                    // (Optional, but good UX. We can rely on user dragging if needed for now)
                }
                popup.Show();
                popup.Activate();
            });
        }

        public void CloseWindow(object viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win.DataContext == viewModel)
                    {
                        win.Close();
                        return;
                    }
                }
            });
        }

        public void ShowLauncherWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var app = Application.Current as App;
                if (app == null) return;
                
                var vm = app.ServiceProvider.GetRequiredService<LauncherViewModel>();
                var window = new LauncherWindow(vm);
                
                vm.RequestClose = () => window.Close();
                
                window.Show();
            });
        }

        #region 贴图功能

        public async Task ShowPinToScreenFromCaptureAsync(PinToScreenOptions? options = null)
        {
            var imageBytes = await ShowCaptureWindowAsync();
            if (imageBytes == null || imageBytes.Length == 0) return;

            try
            {
                var bitmap = LoadBitmapFromBytes(imageBytes);
                if (bitmap != null)
                {
                    ShowPinToScreen(bitmap, options);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"从截图创建贴图失败: {ex.Message}", "WindowManager");
            }
        }

        public bool ShowPinToScreenFromClipboard(PinToScreenOptions? options = null)
        {
            try
            {
                if (WpfClipboard.ContainsImage())
                {
                    var image = WpfClipboard.GetImage();
                    if (image != null)
                    {
                        ShowPinToScreen(image, options);
                        return true;
                    }
                }

                // 尝试从剪贴板获取文件
                if (WpfClipboard.ContainsFileDropList())
                {
                    var files = WpfClipboard.GetFileDropList();
                    if (files.Count > 0)
                    {
                        var file = files[0];
                        if (!string.IsNullOrEmpty(file) && IsImageFile(file))
                        {
                            return ShowPinToScreenFromFile(file, options);
                        }
                    }
                }

                LoggerService.Instance.LogInfo("剪贴板中没有图片", "WindowManager");
                return false;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"从剪贴板创建贴图失败: {ex.Message}", "WindowManager");
                return false;
            }
        }

        public bool ShowPinToScreenFromFile(string filePath, PinToScreenOptions? options = null)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LoggerService.Instance.LogError($"文件不存在: {filePath}", "WindowManager");
                    return false;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();

                ShowPinToScreen(bitmap, options);
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"从文件创建贴图失败: {ex.Message}", "WindowManager");
                return false;
            }
        }

        public void ShowPinToScreen(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null)
        {
            if (image == null) return;

            // 确保图片可跨线程访问
            if (!image.IsFrozen)
            {
                image.Freeze();
            }

            PinToScreenWindow.PinToScreenAsync(image, options, location);
        }

        public void CloseAllPinToScreenWindows()
        {
            PinToScreenWindow.CloseAll();
        }

        public int GetPinToScreenWindowCount()
        {
            return PinToScreenWindow.OpenWindowCount;
        }

        private BitmapSource? LoadBitmapFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private bool IsImageFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || 
                   ext == ".bmp" || ext == ".gif" || ext == ".tiff" || ext == ".ico";
        }

        #endregion
    }
}
