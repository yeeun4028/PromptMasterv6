using Gma.System.MouseKeyHook;
using System;
using System.Windows.Forms; // 需在 csproj 中开启 <UseWindowsForms>true</UseWindowsForms>

namespace PromptMasterv5.Services
{
    public class GlobalKeyService : IDisposable
    {
        // ★ 修复1: 声明为可空类型 (?)
        private IKeyboardMouseEvents? _globalHook;
        private DateTime _lastCtrlPressTime = DateTime.MinValue;
        private const int DoubleClickInterval = 400; // 双击间隔 (毫秒)

        public event EventHandler? OnDoubleCtrlDetected;

        public void Start()
        {
            // 避免重复注册
            if (_globalHook != null) return;

            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyUp += GlobalHook_KeyUp;
        }

        private void GlobalHook_KeyUp(object? sender, KeyEventArgs e)
        {
            // 检测 Ctrl 键
            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.ControlKey)
            {
                var now = DateTime.Now;
                var span = (now - _lastCtrlPressTime).TotalMilliseconds;

                if (span > 50 && span < DoubleClickInterval)
                {
                    OnDoubleCtrlDetected?.Invoke(this, EventArgs.Empty);
                    _lastCtrlPressTime = DateTime.MinValue;
                }
                else
                {
                    _lastCtrlPressTime = now;
                }
            }
        }

        public void Stop()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyUp -= GlobalHook_KeyUp;
                _globalHook.Dispose();
                // ★ 修复2: 现在赋值 null 不会报错了
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