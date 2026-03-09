using PromptMasterv6.Core.Interfaces;
using System.Windows;
using Application = System.Windows.Application;
using PromptMasterv6.Infrastructure.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.IO;
using System.Linq;

using WpfClipboard = System.Windows.Clipboard;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        private readonly IWindowRegistry _windowRegistry;
        private bool _isCapturing = false;

        public WindowManager(IWindowRegistry windowRegistry)
        {
            _windowRegistry = windowRegistry;
        }

        public async Task<byte[]?> ShowCaptureWindowAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null)
        {
            var (bytes, _) = await ShowCaptureWindowWithRectAsync(onCaptureProcessing);
            return bytes;
        }

        private async Task<(byte[]? bytes, System.Windows.Rect rect)> ShowCaptureWindowWithRectAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null)
        {
            if (_isCapturing) return (null, default);
            _isCapturing = true;

            try
            {
                bool needsDelay = false;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var translationPopups = Application.Current.Windows.OfType<Window>()
                        .Where(w => w.GetType().Name == "TranslationPopup" && w.IsLoaded && w.IsVisible)
                        .ToList();
                    foreach (var p in translationPopups) p.Close();
                    if (translationPopups.Count > 0) needsDelay = true;

                    var launchBars = Application.Current.Windows.OfType<Window>()
                        .Where(w => w.GetType().Name == "LaunchBarWindow" && w.IsVisible)
                        .ToList();
                    foreach (var lb in launchBars) lb.Hide();
                    if (launchBars.Count > 0) needsDelay = true;
                });

                if (needsDelay)
                {
                    await Task.Delay(150);
                }

                Bitmap? screenBmp = await ScreenCaptureHelper.CaptureFullScreenAsync();
                if (screenBmp == null) return (null, default);

                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var mainWin = Application.Current.MainWindow;
                    if (mainWin != null) mainWin.GetType().GetProperty("SuppressAutoActivation")?.SetValue(mainWin, true);

                    var capture = _windowRegistry.CreateScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                    screenBmp = null;

                    byte[]? result = null;
                    System.Windows.Rect capturedRect = default;
                    try
                    {
                        if (capture.ShowDialog() == true)
                        {
                            result = capture.GetType().GetProperty("CapturedImageBytes")?.GetValue(capture) as byte[];
                            var rectObj = capture.GetType().GetProperty("CapturedRect")?.GetValue(capture);
                            if (rectObj is System.Windows.Rect r) capturedRect = r;
                        }
                    }
                    finally
                    {
                        var launchBars = Application.Current.Windows.OfType<Window>()
                            .Where(w => w.GetType().Name == "LaunchBarWindow")
                            .ToList();
                        foreach (var lb in launchBars) lb.Show();

                        if (mainWin != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                mainWin.GetType().GetProperty("SuppressAutoActivation")?.SetValue(mainWin, false);
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    return (result, capturedRect);
                });
            }
            finally
            {
                _isCapturing = false;
            }
        }

        public void ShowTranslationPopup(string text, System.Windows.Rect? placementTarget = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = _windowRegistry.CreateTranslationPopup(text);
                if (popup == null) return;

                if (placementTarget.HasValue)
                {
                    var setPlacementMethod = popup.GetType().GetMethod("SetPlacementTarget");
                    setPlacementMethod?.Invoke(popup, new object[] { placementTarget.Value });
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
                var window = _windowRegistry.CreateWindow("LauncherViewModel");
                if (window != null)
                {
                    window.Show();
                }
            });
        }

        public void ShowSettingsWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                var window = _windowRegistry.CreateWindow("SettingsViewModel");
                if (window != null)
                {
                    window.Owner = mainWindow;
                    window.Show();
                }
            });
        }

        public void CloseSettingsWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win.GetType().Name == "SettingsWindow")
                    {
                        win.Close();
                        return;
                    }
                }
            });
        }

        #region PinToScreen

        public async Task ShowPinToScreenFromCaptureAsync(PinToScreenOptions? options = null)
        {
            var (imageBytes, capturedRect) = await ShowCaptureWindowWithRectAsync();
            if (imageBytes == null || imageBytes.Length == 0) return;

            try
            {
                var bitmap = LoadBitmapFromBytes(imageBytes);
                if (bitmap != null)
                {
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

            if (!image.IsFrozen)
            {
                image.Freeze();
            }

            _windowRegistry.PinToScreen(image, options, location);
        }

        public void CloseAllPinToScreenWindows()
        {
            _windowRegistry.CloseAllPinToScreenWindows();
        }

        public int GetPinToScreenWindowCount()
        {
            return _windowRegistry.GetPinToScreenWindowCount();
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
