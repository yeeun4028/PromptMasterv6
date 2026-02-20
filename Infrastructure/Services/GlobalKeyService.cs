using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;

namespace PromptMasterv5.Infrastructure.Services
{
    public class GlobalKeyService : IDisposable
    {
        private IKeyboardMouseEvents? _globalHook;

        private DateTime _lastCtrlPressTime = DateTime.MinValue;
        private const int DoubleClickInterval = 400;

        private string _sequenceBuffer = "";
        private DateTime _lastSequenceTime = DateTime.MinValue;

        public string AlwaysOnTopSequence { get; set; } = "";

        public event EventHandler? OnDoubleCtrlDetected;
        public event EventHandler? OnDoubleSemiColonDetected;
        public event EventHandler? OnAlwaysOnTopSequenceDetected;
        public event EventHandler<System.Windows.Forms.KeyEventArgs>? OnAnyKeyDown;
        public event EventHandler? OnQuickActionTriggered;
        public event EventHandler? OnLauncherTriggered;

        private bool _altPressed = false;
        private bool _ctrlPressed = false;
        private bool _shiftPressed = false;
        private bool _winPressed = false;

        // Configurable launcher hotkey
        private Keys _launcherKey = Keys.S;
        private bool _launcherNeedAlt = true;
        private bool _launcherNeedCtrl = false;
        private bool _launcherNeedShift = false;
        private bool _launcherNeedWin = false;
        private bool _launcherEnabled = true;

        // Configurable quick action hotkey
        private Keys _quickActionKey = Keys.Q;
        private bool _quickActionNeedAlt = true;
        private bool _quickActionNeedCtrl = false;
        private bool _quickActionNeedShift = false;
        private bool _quickActionNeedWin = false;
        private bool _quickActionEnabled = true;

        // Configurable voice hotkey
        private Keys _voiceKey = Keys.T;
        private List<Keys> _voiceModifiers = new List<Keys>();
        private bool _voiceEnabled = false;
        private bool _voiceKeyHeld = false;

        public event EventHandler? OnVoiceControlTriggered;
        public event EventHandler? OnVoiceControlKeyDown;

        public void UpdateVoiceHotkey(string hotkeyStr)
        {
            ParseVoiceHotkey(hotkeyStr);
        }

        // WPF Key.ToString() names → Windows Forms Keys enum names
        private static readonly Dictionary<string, Keys> WpfToWinFormsKeyMap = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
        {
            { "LeftCtrl", Keys.LControlKey },
            { "RightCtrl", Keys.RControlKey },
            { "LeftAlt", Keys.LMenu },
            { "RightAlt", Keys.RMenu },
            { "LeftShift", Keys.LShiftKey },
            { "RightShift", Keys.RShiftKey },
        };

        private void ParseVoiceHotkey(string hotkeyStr)
        {
            _voiceKey = Keys.None;
            _voiceModifiers.Clear();
            _voiceEnabled = false;

            if (string.IsNullOrWhiteSpace(hotkeyStr)) return;

            var parts = hotkeyStr.Split('+');
            foreach (var part in parts)
            {
                var p = part.Trim();

                // Try WPF-to-WinForms mapping first
                Keys k;
                if (!WpfToWinFormsKeyMap.TryGetValue(p, out k))
                {
                    if (!Enum.TryParse<Keys>(p, true, out k))
                        continue; // skip unknown parts
                }

                // Determine if this is a modifier key
                if (k == Keys.LControlKey || k == Keys.RControlKey ||
                    k == Keys.LMenu || k == Keys.RMenu ||
                    k == Keys.LShiftKey || k == Keys.RShiftKey ||
                    k == Keys.LWin || k == Keys.RWin)
                {
                    if (part == parts.Last() && parts.Length == 1)
                    {
                        _voiceKey = k;
                    }
                    else
                    {
                        _voiceModifiers.Add(k);
                    }
                }
                else
                {
                    _voiceKey = k;
                }
            }
            
            _voiceEnabled = _voiceKey != Keys.None;
        }

        private bool CheckVoiceModifiers(Keys eKeyCode)
        {
            // For exact modifier checking, we need to ensure ALL required modifiers are pressed
            // AND NO OTHER modifiers are pressed.
            bool reqLCtrl = _voiceModifiers.Contains(Keys.LControlKey) || _voiceKey == Keys.LControlKey;
            bool reqRCtrl = _voiceModifiers.Contains(Keys.RControlKey) || _voiceKey == Keys.RControlKey;
            bool reqLAlt = _voiceModifiers.Contains(Keys.LMenu) || _voiceKey == Keys.LMenu;
            bool reqRAlt = _voiceModifiers.Contains(Keys.RMenu) || _voiceKey == Keys.RMenu;
            bool reqLShift = _voiceModifiers.Contains(Keys.LShiftKey) || _voiceKey == Keys.LShiftKey;
            bool reqRShift = _voiceModifiers.Contains(Keys.RShiftKey) || _voiceKey == Keys.RShiftKey;
            bool reqLWin = _voiceModifiers.Contains(Keys.LWin) || _voiceKey == Keys.LWin;
            bool reqRWin = _voiceModifiers.Contains(Keys.RWin) || _voiceKey == Keys.RWin;

            bool isLCtrl = NativeMethods.IsKeyDown((int)Keys.LControlKey) || eKeyCode == Keys.LControlKey || (eKeyCode == Keys.ControlKey && reqLCtrl);
            bool isRCtrl = NativeMethods.IsKeyDown((int)Keys.RControlKey) || eKeyCode == Keys.RControlKey || (eKeyCode == Keys.ControlKey && reqRCtrl);
            bool isLAlt = NativeMethods.IsKeyDown((int)Keys.LMenu) || eKeyCode == Keys.LMenu || (eKeyCode == Keys.Menu && reqLAlt);
            bool isRAlt = NativeMethods.IsKeyDown((int)Keys.RMenu) || eKeyCode == Keys.RMenu || (eKeyCode == Keys.Menu && reqRAlt);
            bool isLShift = NativeMethods.IsKeyDown((int)Keys.LShiftKey) || eKeyCode == Keys.LShiftKey || (eKeyCode == Keys.ShiftKey && reqLShift);
            bool isRShift = NativeMethods.IsKeyDown((int)Keys.RShiftKey) || eKeyCode == Keys.RShiftKey || (eKeyCode == Keys.ShiftKey && reqRShift);
            bool isLWin = NativeMethods.IsKeyDown((int)Keys.LWin) || eKeyCode == Keys.LWin;
            bool isRWin = NativeMethods.IsKeyDown((int)Keys.RWin) || eKeyCode == Keys.RWin;

            return reqLCtrl == isLCtrl && reqRCtrl == isRCtrl &&
                   reqLAlt == isLAlt && reqRAlt == isRAlt &&
                   reqLShift == isLShift && reqRShift == isRShift &&
                   reqLWin == isLWin && reqRWin == isRWin;
        }

        public string LauncherHotkeyString
        {
            set => ParseHotkey(value, out _launcherKey, out _launcherNeedCtrl, out _launcherNeedAlt, out _launcherNeedShift, out _launcherNeedWin, out _launcherEnabled);
        }

        public string QuickActionHotkeyString
        {
            set => ParseHotkey(value, out _quickActionKey, out _quickActionNeedCtrl, out _quickActionNeedAlt, out _quickActionNeedShift, out _quickActionNeedWin, out _quickActionEnabled);
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

            // Safety: if the key is a regular key (not F1-F12, etc.) and no modifier is required,
            // disable the hotkey to prevent intercepting normal typing.
            if (enabled && !needCtrl && !needAlt && !needShift && !needWin)
            {
                bool isFunctionKey = key >= Keys.F1 && key <= Keys.F24;
                if (!isFunctionKey)
                {
                    enabled = false;
                    LoggerService.Instance.LogWarning(
                        $"Hotkey '{hotkeyStr}' disabled: regular keys require at least one modifier (Ctrl/Alt/Shift/Win)",
                        "GlobalKeyService.ParseHotkey");
                }
            }
        }

        private bool CheckModifiers(bool needCtrl, bool needAlt, bool needShift, bool needWin)
        {
            return _ctrlPressed == needCtrl && _altPressed == needAlt && _shiftPressed == needShift && _winPressed == needWin;
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
            _globalHook.KeyUp += GlobalHook_KeyUp;
            _globalHook.KeyPress += GlobalHook_KeyPress;
        }

        private void GlobalHook_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Track modifier key states
            if (e.KeyCode == Keys.Menu || e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
                _altPressed = true;
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey)
                _ctrlPressed = true;
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey)
                _shiftPressed = true;
            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                _winPressed = true;

            // Detect Quick Action hotkey
            if (_quickActionEnabled && e.KeyCode == _quickActionKey && CheckModifiers(_quickActionNeedCtrl, _quickActionNeedAlt, _quickActionNeedShift, _quickActionNeedWin))
            {
                OnQuickActionTriggered?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            // Detect Launcher hotkey
            if (_launcherEnabled && e.KeyCode == _launcherKey && CheckModifiers(_launcherNeedCtrl, _launcherNeedAlt, _launcherNeedShift, _launcherNeedWin))
            {
                OnLauncherTriggered?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            // Voice Control: KeyDown = start recording (push-to-talk)
            // Voice control could be a single modifier key itself.
            Keys actualKey = e.KeyCode;
            if (e.KeyCode == Keys.Menu || e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey)
            {
                if (e.KeyCode == Keys.Menu) actualKey = NativeMethods.IsKeyDown((int)Keys.LMenu) ? Keys.LMenu : (NativeMethods.IsKeyDown((int)Keys.RMenu) ? Keys.RMenu : Keys.Menu);
                if (e.KeyCode == Keys.ControlKey) actualKey = NativeMethods.IsKeyDown((int)Keys.LControlKey) ? Keys.LControlKey : (NativeMethods.IsKeyDown((int)Keys.RControlKey) ? Keys.RControlKey : Keys.ControlKey);
                if (e.KeyCode == Keys.ShiftKey) actualKey = NativeMethods.IsKeyDown((int)Keys.LShiftKey) ? Keys.LShiftKey : (NativeMethods.IsKeyDown((int)Keys.RShiftKey) ? Keys.RShiftKey : Keys.ShiftKey);
            }

            if (_voiceEnabled && !_voiceKeyHeld)
            {
                bool isVoiceKeyPress = (actualKey == _voiceKey || e.KeyCode == _voiceKey) || 
                    (e.KeyCode == Keys.Menu && (_voiceKey == Keys.LMenu || _voiceKey == Keys.RMenu)) ||
                    (e.KeyCode == Keys.ControlKey && (_voiceKey == Keys.LControlKey || _voiceKey == Keys.RControlKey)) ||
                    (e.KeyCode == Keys.ShiftKey && (_voiceKey == Keys.LShiftKey || _voiceKey == Keys.RShiftKey));

                if (isVoiceKeyPress && CheckVoiceModifiers(e.KeyCode))
                {
                    _voiceKeyHeld = true;
                    OnVoiceControlKeyDown?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }

            OnAnyKeyDown?.Invoke(this, e);
        }

        private void GlobalHook_KeyUp(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Reset modifier key states
            if (e.KeyCode == Keys.Menu || e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
                _altPressed = false;
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey)
                _ctrlPressed = false;
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey)
                _shiftPressed = false;
            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                _winPressed = false;

            Keys actualKey = e.KeyCode;
            if (e.KeyCode == Keys.Menu || e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey)
            {
                if (e.KeyCode == Keys.Menu) actualKey = NativeMethods.IsKeyDown((int)Keys.LMenu) ? Keys.LMenu : (NativeMethods.IsKeyDown((int)Keys.RMenu) ? Keys.RMenu : Keys.Menu);
                if (e.KeyCode == Keys.ControlKey) actualKey = NativeMethods.IsKeyDown((int)Keys.LControlKey) ? Keys.LControlKey : (NativeMethods.IsKeyDown((int)Keys.RControlKey) ? Keys.RControlKey : Keys.ControlKey);
                if (e.KeyCode == Keys.ShiftKey) actualKey = NativeMethods.IsKeyDown((int)Keys.LShiftKey) ? Keys.LShiftKey : (NativeMethods.IsKeyDown((int)Keys.RShiftKey) ? Keys.RShiftKey : Keys.ShiftKey);
            }

            // Voice Control: KeyUp = stop recording and process (push-to-talk)
            if (_voiceEnabled && _voiceKeyHeld)
            {
                bool isVoiceKeyRelease = (actualKey == _voiceKey || e.KeyCode == _voiceKey) || 
                    (e.KeyCode == Keys.Menu && (_voiceKey == Keys.LMenu || _voiceKey == Keys.RMenu)) ||
                    (e.KeyCode == Keys.ControlKey && (_voiceKey == Keys.LControlKey || _voiceKey == Keys.RControlKey)) ||
                    (e.KeyCode == Keys.ShiftKey && (_voiceKey == Keys.LShiftKey || _voiceKey == Keys.RShiftKey));

                if (isVoiceKeyRelease)
                {
                    _voiceKeyHeld = false;
                    OnVoiceControlTriggered?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }

            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.ControlKey)
            {
                var now = DateTime.Now;
                var span = (now - _lastCtrlPressTime).TotalMilliseconds;

                if (span > 50 && span < DoubleClickInterval)
                {
                    OnDoubleCtrlDetected?.Invoke(this, EventArgs.Empty);
                    _lastCtrlPressTime = DateTime.MinValue;
                }
                else _lastCtrlPressTime = now;
            }
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
                _globalHook.KeyUp -= GlobalHook_KeyUp;
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
