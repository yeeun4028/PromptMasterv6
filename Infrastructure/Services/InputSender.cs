using System;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;

using System.Runtime.InteropServices;

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
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to resolve click point", "InputSender.ResolveClickPoint");
            }

            return (settings.ClickX, settings.ClickY);
        }

        public static async Task SendAsync(string text, InputMode targetMode, LocalSettings settings, IntPtr previousWindowHandle)
        {
            if (string.IsNullOrEmpty(text)) return;

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

            // Use SendInput for text injection
            SendUnicodeString(text);

            await Task.Delay(120);

            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        }

        private static void SendUnicodeString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var inputs = new NativeMethods.INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                var letter = text[i];

                var keyDown = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = letter,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                var keyUp = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = letter,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                inputs[i * 2] = keyDown;
                inputs[i * 2 + 1] = keyUp;
            }

            NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        }


    }
}
