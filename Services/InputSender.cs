using System;
using System.Threading.Tasks;
using PromptMasterv5.Models;

// ★★★ 修复关键：显式指定引用，解决 WPF 和 WinForms 的冲突 ★★★
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5.Services
{
    public class InputSender
    {
        // 执行发送流程
        public static async Task SendAsync(string text, LocalSettings settings, IntPtr previousWindowHandle)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 1. 将文本写入剪贴板 (带重试机制)
            bool clipboardSuccess = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    // 这里现在明确使用 System.Windows.Clipboard
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

            // 2. 等待主窗口隐藏动画完成
            await Task.Delay(200);

            // 3. 根据模式处理焦点
            if (settings.Mode == InputMode.SmartFocus)
            {
                // 模式A：智能回退
                if (previousWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(previousWindowHandle);
                    await Task.Delay(150);
                }
            }
            else
            {
                // 模式B：坐标点击
                NativeMethods.SetCursorPos(settings.ClickX, settings.ClickY);
                await Task.Delay(50);

                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(50);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                await Task.Delay(200);
            }

            // 4. 模拟 Ctrl + V
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);

            // 5. 等待粘贴完成
            await Task.Delay(100);

            // 6. 模拟 Enter 发送
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, 0, 0);
            await Task.Delay(20);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        }
    }
}