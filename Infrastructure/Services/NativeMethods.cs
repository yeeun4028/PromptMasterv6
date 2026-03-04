using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PromptMasterv5.Infrastructure.Services
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        // ---------------------------------------------------------------
        // SendInput — 推荐用于模拟键盘/鼠标事件，替代废弃的 mouse_event/keybd_event
        // ---------------------------------------------------------------

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public const int INPUT_MOUSE    = 0;
        public const int INPUT_KEYBOARD = 1;

        public const uint KEYEVENTF_KEYUP       = 0x0002;
        public const uint KEYEVENTF_SCANCODE     = 0x0008;
        public const uint KEYEVENTF_EXTENDEDKEY  = 0x0001;
        public const uint KEYEVENTF_UNICODE      = 0x0004;

        public const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        public const uint MOUSEEVENTF_MOVE      = 0x0001;
        public const uint MOUSEEVENTF_ABSOLUTE  = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int      dx;
            public int      dy;
            public uint     mouseData;
            public uint     dwFlags;
            public uint     time;
            public IntPtr   dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort   wVk;
            public ushort   wScan;
            public uint     dwFlags;
            public uint     time;
            public IntPtr   dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint     uMsg;
            public ushort   wParamL;
            public ushort   wParamH;
        }

        // 使用显式布局的 Union，代替 C++ 中的 union { MOUSEINPUT; KEYBDINPUT; HARDWAREINPUT; }
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT_UNION
        {
            [FieldOffset(0)] public MOUSEINPUT    mi;
            [FieldOffset(0)] public KEYBDINPUT    ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int        type;
            public INPUT_UNION u;
        }

        /// <summary>
        /// 发送一次键盘按键（按下 + 弹起）
        /// </summary>
        public static void SendKey(byte vk, bool keyUp = false)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk     = vk,
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                    }
                }
            };
            SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        }

        // ---------------------------------------------------------------
        // Virtual Key Constants
        // ---------------------------------------------------------------
        public const byte VK_CONTROL = 0x11;
        public const byte VK_SHIFT   = 0x10;
        public const byte VK_MENU    = 0x12; // ALT
        public const byte VK_LWIN    = 0x5B;
        public const byte VK_C       = 0x43;
        public const byte VK_V       = 0x56;

        // ---------------------------------------------------------------
        // WinEvent Hook
        // ---------------------------------------------------------------
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public static bool IsKeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

        // ---------------------------------------------------------------
        // Single Instance Support
        // ---------------------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        public const int SW_RESTORE = 9;
        public const int SW_SHOW    = 5;

        // ---------------------------------------------------------------
        // Window Long — 使用 64 位兼容版本 GetWindowLongPtr / SetWindowLongPtr
        // ---------------------------------------------------------------
        public const int GWL_EXSTYLE    = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // ---------------------------------------------------------------
        // System Metrics for Screen Capture & Full-Screen Detection
        // ---------------------------------------------------------------
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        public const int SM_CXSCREEN       = 0;  // 主显示器宽度（物理像素）
        public const int SM_CYSCREEN       = 1;  // 主显示器高度（物理像素）
        public const int SM_XVIRTUALSCREEN  = 76;
        public const int SM_YVIRTUALSCREEN  = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        /// <summary>
        /// 检测当前前台窗口是否处于全屏模式（基于主显示器）
        /// </summary>
        public static bool IsForegroundFullScreen()
        {
            var desktopHandle = GetDesktopWindow();
            var shellHandle   = GetShellWindow();
            var handle        = GetForegroundWindow();

            if (handle.Equals(IntPtr.Zero)) return false;
            if (handle.Equals(desktopHandle) || handle.Equals(shellHandle)) return false;

            GetWindowRect(handle, out RECT rect);

            // 允许 2 像素误差（部分全屏应用存在微小边距）
            return rect.left   <= 2
                && rect.top    <= 2
                && rect.right  >= GetSystemMetrics(SM_CXSCREEN) - 2
                && rect.bottom >= GetSystemMetrics(SM_CYSCREEN) - 2;
        }
    }
}
