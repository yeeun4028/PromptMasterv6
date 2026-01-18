using System;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using System.Windows.Automation;

// 解决引用冲突
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5.Services
{
    public class InputSender
    {
        private static (int x, int y) ResolveClickPoint(LocalSettings settings, IntPtr previousWindowHandle)
        {
            try
            {
                string url = TryGetBrowserUrl(previousWindowHandle);

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

        private static string TryGetBrowserUrl(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return "";

                var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                var elementCollection = root.FindAll(TreeScope.Descendants, editCondition);
                foreach (AutomationElement element in elementCollection)
                {
                    if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj))
                    {
                        var valuePattern = (ValuePattern)patternObj;
                        string value = valuePattern.Current.Value;
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
            }
            catch { }
            return "";
        }

        // ★★★ 修改：新增 targetMode 参数，不再依赖 settings.Mode ★★★
        public static async Task SendAsync(string text, InputMode targetMode, LocalSettings settings, IntPtr previousWindowHandle)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 1. 写入剪贴板
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

            // 2. 等待窗口隐藏
            await Task.Delay(200);

            // 3. 根据传入的 targetMode 执行不同逻辑
            if (targetMode == InputMode.SmartFocus)
            {
                // === 智能回退模式 (Ctrl+Enter / 列表双击) ===
                if (previousWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(previousWindowHandle);
                    await Task.Delay(150);
                }
            }
            else
            {
                // === 坐标点击模式 (双击 Enter) ===
                var (x, y) = ResolveClickPoint(settings, previousWindowHandle);
                NativeMethods.SetCursorPos(x, y);
                await Task.Delay(50);

                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(50);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                // 等待网页响应点击焦点
                await Task.Delay(200);
            }

            // 4. 模拟 Ctrl+V
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);

            // 5. 模拟 Enter
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
