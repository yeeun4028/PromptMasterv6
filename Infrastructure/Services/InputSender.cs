using System;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;

using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5.Infrastructure.Services
{
    public class InputSender
    {
        private static (int x, int y) ResolveClickPoint(LocalSettings settings, IntPtr previousWindowHandle)
        {
            try
            {
                string url = BrowserUrlDetector.TryGetChromeOrEdgeAddressBarUrl(previousWindowHandle);

                if (settings.CoordinateRules != null && settings.CoordinateRules.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        foreach (var rule in settings.CoordinateRules)
                        {
                            if (string.IsNullOrWhiteSpace(rule.UrlContains)) continue;
                            if (url.Contains(rule.UrlContains.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                return (rule.X, rule.Y);
                            }
                        }
                    }

                    foreach (var rule in settings.CoordinateRules)
                    {
                        if (string.IsNullOrWhiteSpace(rule.UrlContains))
                        {
                            return (rule.X, rule.Y);
                        }
                    }

                    var first = settings.CoordinateRules[0];
                    return (first.X, first.Y);
                }
            }
            catch { }

            return (settings.ClickX, settings.ClickY);
        }

        public static async Task SendAsync(string text, InputMode targetMode, LocalSettings settings, IntPtr previousWindowHandle)
        {
            if (string.IsNullOrEmpty(text)) return;

            bool clipboardSuccess = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    clipboardSuccess = true;
                    break;
                }
                catch
                {
                    await Task.Delay(50);
                }
            }

            if (!clipboardSuccess)
            {
                MessageBox.Show("写入剪贴板失败，无法发送。", "错误");
                return;
            }

            await Task.Delay(200);

            if (targetMode == InputMode.SmartFocus)
            {
                if (previousWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(previousWindowHandle);
                    await Task.Delay(150);
                }
            }
            else
            {
                var (x, y) = ResolveClickPoint(settings, previousWindowHandle);
                NativeMethods.SetCursorPos(x, y);
                await Task.Delay(50);

                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(50);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                await Task.Delay(200);
            }

            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);

            await Task.Delay(120);
            for (int i = 0; i < 30; i++)
            {
                if (!NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL) &&
                    !NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT) &&
                    !NativeMethods.IsKeyDown(NativeMethods.VK_MENU))
                {
                    break;
                }
                await Task.Delay(20);
            }

            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        }
    }
}
