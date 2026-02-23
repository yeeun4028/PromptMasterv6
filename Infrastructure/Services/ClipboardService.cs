using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;
using FormsClipboard = System.Windows.Forms.Clipboard;
using System.Windows.Forms; // For SendKeys
using System.Windows.Automation; // For UI Automation

namespace PromptMasterv5.Infrastructure.Services
{
    /// <summary>
    /// 剪贴板操作服务
    /// </summary>
    public class ClipboardService
    {
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;
        private const int VK_V = 0x56;
        private const int KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// 模拟 Ctrl+C 获取当前选中的文本 (功能已移除)
        /// </summary>
        /// <returns>选中的文本，如果失败则返回 null</returns>
        public async Task<string?> GetSelectedTextAsync()
        {
            // Note: Implementation intentionally removed as part of 'Selection Assistant' removal.
            return await Task.FromResult<string?>(null);
        }

        /// <summary>
        /// 将文本设置到剪贴板
        /// </summary>
        public void SetClipboard(string text)
        {
            try
            {
                // 确保在 UI 线程执行
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        FormsClipboard.SetText(text);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogError($"设置剪贴板失败内部错误: {ex.Message}", "ClipboardService.SetClipboard");
                    }
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"设置剪贴板失败: {ex.Message}", "ClipboardService.SetClipboard");
            }
        }

        /// <summary>
        /// 模拟 Ctrl+V 将剪贴板内容粘贴到当前活跃窗口
        /// </summary>
        public void PasteToActiveWindow()
        {
            try
            {
                // 等待 100ms 确保窗口焦点已恢复
                Thread.Sleep(100);

                NativeMethods.keybd_event(VK_CONTROL, 0, 0, 0);
                NativeMethods.keybd_event(VK_V, 0, 0, 0);
                NativeMethods.keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
                NativeMethods.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"粘贴失败: {ex.Message}", "ClipboardService.PasteToActiveWindow");
            }
        }
    }
}
