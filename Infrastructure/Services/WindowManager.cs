using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Views;
using PromptMasterv6.ViewModels;
using System.Windows;
using Application = System.Windows.Application;
using PromptMasterv6.Infrastructure.Helpers;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media.Imaging;
using PromptMasterv6.Core.Models;
using System.IO;

using WpfClipboard = System.Windows.Clipboard;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        public async Task<byte[]?> ShowCaptureWindowAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null)
        {
            // 内部调用带定位信息的重载，丢弃 rect
            var (bytes, _) = await ShowCaptureWindowWithRectAsync(onCaptureProcessing);
            return bytes;
        }

        private async Task<(byte[]? bytes, System.Windows.Rect rect)> ShowCaptureWindowWithRectAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null)
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
                return (null, default);
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
                var launchBarWindows = new System.Collections.Generic.List<LaunchBarWindow>();
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is LaunchBarWindow lbw && lbw.IsVisible)
                    {
                        launchBarWindows.Add(lbw);
                        lbw.Hide();
                    }
                }

                // 将 screenBmp 的所有权转交给 ScreenCaptureOverlay，
                // 由它在使用后负责释放（OnClosed 中 Dispose）。
                // WindowManager 不持有引用，彻底杜绝此处的位图泄漏。
                var capture = new ScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                screenBmp = null; // 显式释放引用，所有权已转交

                byte[]? result = null;
                System.Windows.Rect capturedRect = default;
                try
                {
                    if (capture.ShowDialog() == true)
                    {
                        result = capture.CapturedImageBytes;
                        capturedRect = capture.CapturedRect;
                    }
                }
                finally
                {
                    // Restore LaunchBarWindow visibility after capture dialog closes
                    foreach (var lbw in launchBarWindows)
                    {
                        lbw.Show();
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
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainWin.SuppressAutoActivation = false;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                return (result, capturedRect);
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
                    if (win is Views.SettingsWindow settingsWin && viewModel is ViewModels.MainViewModel)
                    {
                        settingsWin.Close();
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

        public void ShowSettingsWindow(object viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                var window = new SettingsWindow();
                
                if (viewModel is ViewModels.MainViewModel mainVM)
                {
                    window.SetDataContext(mainVM);
                }
                
                window.Owner = mainWindow;
                window.Show();
            });
        }

        public void CloseSettingsWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is SettingsWindow settingsWin)
                    {
                        settingsWin.Close();
                        return;
                    }
                }
            });
        }

        #region 贴图功能

        public async Task ShowPinToScreenFromCaptureAsync(PinToScreenOptions? options = null)
        {
            var (imageBytes, capturedRect) = await ShowCaptureWindowWithRectAsync();
            if (imageBytes == null || imageBytes.Length == 0) return;

            try
            {
                var bitmap = LoadBitmapFromBytes(imageBytes);
                if (bitmap != null)
                {
                    // 将贴图窗口定位于用户框选区域的左上角
                    var location = capturedRect == default
                        ? (System.Windows.Point?)null
                        : new System.Windows.Point(capturedRect.X, capturedRect.Y);
                    ShowPinToScreen(bitmap, options, location);
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

            LoggerService.Instance.LogInfo(
                $"[PIN-DIAG] LoadBitmapFromBytes: PixelWidth={bitmap.PixelWidth}, PixelHeight={bitmap.PixelHeight}, " +
                $"DpiX={bitmap.DpiX}, DpiY={bitmap.DpiY}, " +
                $"Width(DIPs)={bitmap.Width}, Height(DIPs)={bitmap.Height}",
                "WindowManager");

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
