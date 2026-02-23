using NHotkey;
using NHotkey.Wpf;
using System;
using System.Windows.Input;

namespace PromptMasterv5.Infrastructure.Services
{
    /// <summary>
    /// 热键服务
    /// 负责全局热键的注册和管理
    /// </summary>
    public class HotkeyService
    {
        /// <summary>
        /// 注册或更新窗口热键
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <param name="hotkeyStr">热键字符串，如 "Ctrl+Shift+P"</param>
        /// <param name="action">触发时执行的操作</param>
        public void RegisterWindowHotkey(string name, string hotkeyStr, Action action)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hotkeyStr))
                {
                    TryRemoveHotkey(name);
                    return;
                }

                ModifierKeys modifiers = ModifierKeys.None;
                if (hotkeyStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
                if (hotkeyStr.Contains("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
                if (hotkeyStr.Contains("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
                if (hotkeyStr.Contains("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;

                string keyStr = hotkeyStr.Split('+')[^1].Trim();
                if (Enum.TryParse(keyStr, true, out Key key))
                {
                    TryRemoveHotkey(name);
                    HotkeyManager.Current.AddOrReplace(name, key, modifiers, (_, __) => action());
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to register hotkey: {name}", "HotkeyService.RegisterWindowHotkey");
            }
        }

        /// <summary>
        /// 移除热键
        /// </summary>
        public void TryRemoveHotkey(string name)
        {
            try
            {
                HotkeyManager.Current.Remove(name);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to remove hotkey: {name}", "HotkeyService.TryRemoveHotkey");
            }
        }
    }
}
