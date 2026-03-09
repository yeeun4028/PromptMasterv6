using System;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IHotkeyService
    {
        void RegisterWindowHotkey(string name, string hotkeyStr, Action action);
        void TryRemoveHotkey(string name);
        void SimulateHotkey(string hotkeyStr);
    }
}
