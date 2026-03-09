using NHotkey;
using NHotkey.Wpf;
using System;
using System.Windows.Input;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class HotkeyService : IHotkeyService
    {
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
