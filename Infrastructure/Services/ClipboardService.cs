using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;
using FormsClipboard = System.Windows.Forms.Clipboard;
using System.Windows.Forms; // For SendKeys
using System.Windows.Automation; // For UI Automation

namespace PromptMasterv6.Infrastructure.Services
{
    /// <summary>
    /// 剪贴板操作服务
    /// </summary>
    public class ClipboardService
    {
        // 虚拟键码现统一由 NativeMethods 提供

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

                // 使用 SendInput 模拟 Ctrl+V（替代废弃的 keybd_event）
                NativeMethods.SendKey(NativeMethods.VK_CONTROL);
                NativeMethods.SendKey(NativeMethods.VK_V);
                NativeMethods.SendKey(NativeMethods.VK_V,   keyUp: true);
                NativeMethods.SendKey(NativeMethods.VK_CONTROL, keyUp: true);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"粘贴失败: {ex.Message}", "ClipboardService.PasteToActiveWindow");
            }
        }
    }
}
