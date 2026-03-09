using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;
using FormsClipboard = System.Windows.Forms.Clipboard;
using System.Windows.Forms;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class ClipboardService : IClipboardService
    {
        public void SetClipboard(string text)
        {
            try
            {
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

        public string? GetClipboard()
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    return WpfClipboard.ContainsText() ? WpfClipboard.GetText() : null;
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"获取剪贴板失败: {ex.Message}", "ClipboardService.GetClipboard");
                return null;
            }
        }

        public bool ContainsText()
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => WpfClipboard.ContainsText());
            }
            catch
            {
                return false;
            }
        }

        public bool ContainsImage()
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => WpfClipboard.ContainsImage());
            }
            catch
            {
                return false;
            }
        }

        public System.Windows.Media.Imaging.BitmapSource? GetImage()
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    return WpfClipboard.ContainsImage() ? WpfClipboard.GetImage() : null;
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"获取剪贴板图片失败: {ex.Message}", "ClipboardService.GetImage");
                return null;
            }
        }

        public void PasteToActiveWindow()
        {
            try
            {
                Thread.Sleep(100);
                NativeMethods.SendKey(NativeMethods.VK_CONTROL);
                NativeMethods.SendKey(NativeMethods.VK_V);
                NativeMethods.SendKey(NativeMethods.VK_V, keyUp: true);
                NativeMethods.SendKey(NativeMethods.VK_CONTROL, keyUp: true);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"粘贴失败: {ex.Message}", "ClipboardService.PasteToActiveWindow");
            }
        }

        public async Task<string?> GetSelectedTextAsync()
        {
            return await Task.FromResult<string?>(null);
        }
    }
}
