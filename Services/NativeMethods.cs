using System;
using System.Runtime.InteropServices;

namespace PromptMasterv5.Services
{
    public static class NativeMethods
    {
        // 获取当前活动窗口句柄
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        // 设置窗口为前台（激活窗口）
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // 设置鼠标位置
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        // 模拟鼠标点击事件
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        // 模拟键盘事件
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // 常量定义
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public const byte VK_CONTROL = 0x11;
        public const byte VK_RETURN = 0x0D;
        public const byte VK_V = 0x56;

        public const uint KEYEVENTF_KEYUP = 0x0002;
    }
}