using System;
using System.Diagnostics;

namespace PromptMasterv5.Infrastructure.Services;

public static class BrowserUrlDetector
{
    private static IntPtr _lastProcHwnd = IntPtr.Zero;
    private static bool _lastIsBrowser = false;
    private static DateTime _lastProcCheckTime = DateTime.MinValue;

    public static bool IsChromeOrEdgeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        var now = DateTime.UtcNow;
        if (hwnd == _lastProcHwnd && (now - _lastProcCheckTime).TotalSeconds < 5.0)
        {
            return _lastIsBrowser;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName.ToLowerInvariant();
            var isBrowser = name is "chrome" or "msedge";

            _lastProcHwnd = hwnd;
            _lastIsBrowser = isBrowser;
            _lastProcCheckTime = now;

            return isBrowser;
        }
        catch
        {
            return false;
        }
    }

    // UI Automation URL detection removed to reduce CPU usage
    public static string TryGetChromeOrEdgeAddressBarUrl(IntPtr hwnd)
    {
        return "";
    }
}

