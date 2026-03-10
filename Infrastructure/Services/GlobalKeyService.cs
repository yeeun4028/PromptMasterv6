using Gma.System.MouseKeyHook;
using System;
using System.Linq;
using System.Windows.Forms;

namespace PromptMasterv6.Infrastructure.Services
{
    public class GlobalKeyService
    {
        private readonly LoggerService _logger;
        private IKeyboardMouseEvents? _globalHook;

        private string _sequenceBuffer = "";
        private DateTime _lastSequenceTime = DateTime.MinValue;

        public GlobalKeyService(LoggerService logger)
        {
            _logger = logger;
        }

        public string AlwaysOnTopSequence { get; set; } = "";

        public event EventHandler? OnDoubleSemiColonDetected;
        public event EventHandler? OnAlwaysOnTopSequenceDetected;
        public event EventHandler<System.Windows.Forms.KeyEventArgs>? OnAnyKeyDown;
        public event EventHandler? OnLauncherTriggered;

        private Keys _launcherKey = Keys.S;
        private bool _launcherNeedAlt = true;
        private bool _launcherNeedCtrl = false;
        private bool _launcherNeedShift = false;
        private bool _launcherNeedWin = false;
        private bool _launcherEnabled = true;

        public string LauncherHotkeyString
        {
            set => ParseHotkey(value, out _launcherKey, out _launcherNeedCtrl, out _launcherNeedAlt, out _launcherNeedShift, out _launcherNeedWin, out _launcherEnabled);
        }

        private void ParseHotkey(string hotkeyStr, out Keys key, out bool needCtrl, out bool needAlt, out bool needShift, out bool needWin, out bool enabled)
        {
            key = Keys.None;
            needCtrl = false;
            needAlt = false;
            needShift = false;
            needWin = false;
            enabled = false;

            if (string.IsNullOrWhiteSpace(hotkeyStr)) return;

            var parts = hotkeyStr.Split('+');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) needCtrl = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) needAlt = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) needShift = true;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) needWin = true;
                else if (Enum.TryParse<Keys>(p, true, out var k)) key = k;
            }
            enabled = key != Keys.None;

            if (enabled && !needCtrl && !needAlt && !needShift && !needWin)
            {
                bool isFunctionKey = key >= Keys.F1 && key <= Keys.F24;
                if (!isFunctionKey)
                {
                    enabled = false;
                    _logger.LogWarning(
                        $"Hotkey '{hotkeyStr}' disabled: regular keys require at least one modifier (Ctrl/Alt/Shift/Win)",
                        "GlobalKeyService.ParseHotkey");
                }
            }
        }

        private bool CheckModifiers(bool needCtrl, bool needAlt, bool needShift, bool needWin)
        {
            bool isCtrlPressed = NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL);
            bool isAltPressed = NativeMethods.IsKeyDown(NativeMethods.VK_MENU);
            bool isShiftPressed = NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT);
            bool isWinPressed = NativeMethods.IsKeyDown(NativeMethods.VK_LWIN);

            return isCtrlPressed == needCtrl && isAltPressed == needAlt && 
                   isShiftPressed == needShift && isWinPressed == needWin;
        }

        private static char NormalizeSymbol(char c)
        {
            return c switch
            {
                '；' => ';',
                '＇' => '\'',
                '\u2018' => '\'',
                '\u2019' => '\'',
                '`' => '\'',
                '´' => '\'',
                _ => c
            };
        }

        public void Start()
        {
            if (_globalHook != null) return;

            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyDown += GlobalHook_KeyDown;
            _globalHook.KeyPress += GlobalHook_KeyPress;
        }

        private void GlobalHook_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (_launcherEnabled && e.KeyCode == _launcherKey && CheckModifiers(_launcherNeedCtrl, _launcherNeedAlt, _launcherNeedShift, _launcherNeedWin))
            {
                OnLauncherTriggered?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            OnAnyKeyDown?.Invoke(this, e);
        }

        private void GlobalHook_KeyPress(object? sender, KeyPressEventArgs e)
        {
            var now = DateTime.Now;
            var normalizedChar = NormalizeSymbol(e.KeyChar);

            if ((now - _lastSequenceTime).TotalMilliseconds > 800)
            {
                _sequenceBuffer = "";
            }

            _lastSequenceTime = now;
            _sequenceBuffer += normalizedChar;

            if (_sequenceBuffer.Length > 32)
            {
                _sequenceBuffer = _sequenceBuffer.Substring(_sequenceBuffer.Length - 32);
            }

            if (_sequenceBuffer.EndsWith(";;", StringComparison.Ordinal))
            {
                OnDoubleSemiColonDetected?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                _sequenceBuffer = "";
                _lastSequenceTime = DateTime.MinValue;
                return;
            }

            if (!string.IsNullOrWhiteSpace(AlwaysOnTopSequence))
            {
                var normalizedSequence = new string(AlwaysOnTopSequence.Select(NormalizeSymbol).ToArray());
                if (_sequenceBuffer.EndsWith(normalizedSequence, StringComparison.Ordinal))
                {
                    OnAlwaysOnTopSequenceDetected?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    _sequenceBuffer = "";
                    _lastSequenceTime = DateTime.MinValue;
                }
            }
        }

        public void Stop()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyDown -= GlobalHook_KeyDown;
                _globalHook.KeyPress -= GlobalHook_KeyPress;
                _globalHook.Dispose();
                _globalHook = null;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
