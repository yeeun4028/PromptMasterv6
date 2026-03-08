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
using System.Linq;

using WpfClipboard = System.Windows.Clipboard;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WindowManager : IWindowManager
    {
        private bool _isCapturing = false;

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
                    var popups = Application.Current.Windows.OfType<TranslationPopup>().Where(p => p.IsLoaded && !p.IsClosing).ToList();
                    foreach (var p in popups) p.Close();
                    if (popups.Count > 0) needsDelay = true;

                    var launchBars = Application.Current.Windows.OfType<LaunchBarWindow>().Where(w => w.IsVisible).ToList();
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
                    var mainWin = Application.Current.MainWindow as MainWindow;
                    if (mainWin != null) mainWin.SuppressAutoActivation = true;

                    var capture = new ScreenCaptureOverlay(screenBmp, onCaptureProcessing);
                    screenBmp = null;

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
                        var launchBars = Application.Current.Windows.OfType<LaunchBarWindow>().ToList();
                        foreach (var lb in launchBars) lb.Show();

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
            finally
            {
                _isCapturing = false;
            }
        }

        public void ShowTranslationPopup(string text, System.Windows.Rect? placementTarget = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = new TranslationPopup(text);
                if (placementTarget.HasValue)
                {
                    popup.SetPlacementTarget(placementTarget.Value);
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

        public void ShowSettingsWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                var window = new SettingsWindow();
                
                var app = Application.Current as App;
                var settingsVM = app?.ServiceProvider.GetRequiredService<SettingsViewModel>();
                if (settingsVM != null)
                {
                    window.SetDataContext(settingsVM);
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
