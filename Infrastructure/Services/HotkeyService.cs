using NHotkey;
using NHotkey.Wpf;
using System;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Services
{
    public class HotkeyService
    {
        private readonly LoggerService _logger;

        public HotkeyService(LoggerService logger)
        {
            _logger = logger;
        }

        public bool RegisterWindowHotkey(string name, string hotkeyStr, Action action)
        {
            if (string.IsNullOrWhiteSpace(hotkeyStr))
            {
                TryRemoveHotkey(name);
                return true;
            }

            try
            {
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
                    return true;
                }
                
                return false;
            }
            catch (HotkeyAlreadyRegisteredException ex)
            {
                _logger.LogException(ex, $"快捷键被占用: {name} ({hotkeyStr})", "HotkeyService.RegisterWindowHotkey");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"注册快捷键遭遇未知错误: {name}", "HotkeyService.RegisterWindowHotkey");
                return false;
            }
        }

        public void TryRemoveHotkey(string name)
        {
            try
            {
                HotkeyManager.Current.Remove(name);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"Failed to remove hotkey: {name}", "HotkeyService.TryRemoveHotkey");
            }
        }

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

                if (modifiers.HasFlag(ModifierKeys.Control)) NativeMethods.SendKey(NativeMethods.VK_CONTROL);
                if (modifiers.HasFlag(ModifierKeys.Alt))     NativeMethods.SendKey(NativeMethods.VK_MENU);
                if (modifiers.HasFlag(ModifierKeys.Shift))   NativeMethods.SendKey(NativeMethods.VK_SHIFT);
                if (modifiers.HasFlag(ModifierKeys.Windows)) NativeMethods.SendKey(NativeMethods.VK_LWIN);

                NativeMethods.SendKey(vk);
                NativeMethods.SendKey(vk, keyUp: true);

                if (modifiers.HasFlag(ModifierKeys.Windows)) NativeMethods.SendKey(NativeMethods.VK_LWIN,    keyUp: true);
                if (modifiers.HasFlag(ModifierKeys.Shift))   NativeMethods.SendKey(NativeMethods.VK_SHIFT,   keyUp: true);
                if (modifiers.HasFlag(ModifierKeys.Alt))     NativeMethods.SendKey(NativeMethods.VK_MENU,    keyUp: true);
                if (modifiers.HasFlag(ModifierKeys.Control)) NativeMethods.SendKey(NativeMethods.VK_CONTROL, keyUp: true);
            }
        }
    }
}
