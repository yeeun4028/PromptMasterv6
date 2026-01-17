using Gma.System.MouseKeyHook;
using System;
using System.Linq;
using System.Windows.Forms;

namespace PromptMasterv5.Services
{
    public class GlobalKeyService : IDisposable
    {
        private IKeyboardMouseEvents? _globalHook;

        // Ctrl 双击相关
        private DateTime _lastCtrlPressTime = DateTime.MinValue;
        private const int DoubleClickInterval = 400;

        private string _sequenceBuffer = "";
        private DateTime _lastSequenceTime = DateTime.MinValue;

        public string AlwaysOnTopSequence { get; set; } = "";

        public event EventHandler? OnDoubleCtrlDetected;
        public event EventHandler? OnDoubleSemiColonDetected;
        public event EventHandler? OnAlwaysOnTopSequenceDetected;

        private static char NormalizeSymbol(char c)
        {
            return c switch
            {
                '；' => ';',
                '＇' => '\'',
                '‘' => '\'',
                '’' => '\'',
                '`' => '\'',
                '´' => '\'',
                _ => c
            };
        }

        public void Start()
        {
            if (_globalHook != null) return;

            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyUp += GlobalHook_KeyUp;
            // 订阅字符输入事件
            _globalHook.KeyPress += GlobalHook_KeyPress;
        }

        private void GlobalHook_KeyUp(object? sender, KeyEventArgs e)
        {
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
