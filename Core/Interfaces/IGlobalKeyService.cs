using System;
using System.Windows.Forms;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IGlobalKeyService : IDisposable
    {
        string AlwaysOnTopSequence { get; set; }
        string LauncherHotkeyString { set; }

        event EventHandler? OnDoubleSemiColonDetected;
        event EventHandler? OnAlwaysOnTopSequenceDetected;
        event EventHandler<KeyEventArgs>? OnAnyKeyDown;
        event EventHandler? OnLauncherTriggered;

        void Start();
        void Stop();
    }
}
