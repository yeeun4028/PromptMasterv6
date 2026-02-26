using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Views;
using PromptMasterv5.ViewModels;
using System.Windows;
using Application = System.Windows.Application;
using PromptMasterv5.Infrastructure.Helpers;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
