using NHotkey;
using NHotkey.Wpf;
using System;
using System.Runtime.InteropServices;
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// 模拟按下指定的热键组合 (例如 "Alt+Space")
        /// </summary>
        public void SimulateHotkey(string hotkeyStr)
        {
            if (string.IsNullOrWhiteSpace(hotkeyStr)) return;

            ModifierKeys modifiers = ModifierKeys.None;
            if (hotkeyStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
            if (hotkeyStr.Contains("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
            if (hotkeyStr.Contains("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
            if (hotkeyStr.Contains("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;

            string keyStr = hotkeyStr.Split('+')[^1].Trim();
            if (Enum.TryParse(keyStr, true, out Key key))
            {
                byte vk = (byte)KeyInterop.VirtualKeyFromKey(key);

                // 按下修饰键
                if (modifiers.HasFlag(ModifierKeys.Control)) keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL
                if (modifiers.HasFlag(ModifierKeys.Alt)) keybd_event(0x12, 0, 0, UIntPtr.Zero); // VK_MENU
                if (modifiers.HasFlag(ModifierKeys.Shift)) keybd_event(0x10, 0, 0, UIntPtr.Zero); // VK_SHIFT
                if (modifiers.HasFlag(ModifierKeys.Windows)) keybd_event(0x5B, 0, 0, UIntPtr.Zero); // VK_LWIN

                // 按下目标键
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                // 松开目标键
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // 松开修饰键（反向）
                if (modifiers.HasFlag(ModifierKeys.Windows)) keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                if (modifiers.HasFlag(ModifierKeys.Shift)) keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                if (modifiers.HasFlag(ModifierKeys.Alt)) keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                if (modifiers.HasFlag(ModifierKeys.Control)) keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
    }
}
