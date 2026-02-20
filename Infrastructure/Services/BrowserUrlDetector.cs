using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;

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

    private static IntPtr _lastUrlHwnd = IntPtr.Zero;
    private static string _lastTitle = "";
    private static string _lastUrl = "";
    private static DateTime _lastUrlDetectTime = DateTime.MinValue;

    public static string TryGetChromeOrEdgeAddressBarUrl(IntPtr hwnd)
    {
        if (!IsChromeOrEdgeWindow(hwnd)) return "";

        var sb = new System.Text.StringBuilder(256);
        if (NativeMethods.GetWindowText(hwnd, sb, sb.Capacity) == 0) return "";
        var currentTitle = sb.ToString();

        var now = DateTime.UtcNow;
        // The most expensive operation is FindAll on UI Automation tree.
        // We can safely cache the result for the same window and title for a short period (e.g. 5 seconds)
        // because users don't typically change URLs without the window title changing or a significant time gap.
        if (hwnd == _lastUrlHwnd && _lastTitle == currentTitle && (now - _lastUrlDetectTime).TotalSeconds < 5.0)
        {
            return _lastUrl;
        }

        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return "";

            var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var edits = root.FindAll(TreeScope.Descendants, editCondition);
            if (edits == null || edits.Count == 0) return "";

            string bestValue = "";
            double bestScore = double.NegativeInfinity;

            foreach (AutomationElement element in edits)
            {
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj)) continue;
                var value = ((ValuePattern)patternObj).Current.Value;
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (value.Any(char.IsWhiteSpace)) continue;
                if (!LooksLikeUrlOrDomain(value)) continue;

                var rect = element.Current.BoundingRectangle;
                if (rect.IsEmpty) continue;
                if (rect.Height < 10 || rect.Height > 80) continue;
                if (rect.Width < 350) continue;
                if (rect.Top > 260) continue;

                var name = element.Current.Name ?? "";
                var automationId = element.Current.AutomationId ?? "";
                var hintBoost =
                    (name.Contains("address", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("search", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("地址", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("网址", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("搜索", StringComparison.OrdinalIgnoreCase) ||
                     automationId.Contains("address", StringComparison.OrdinalIgnoreCase))
                        ? 2000
                        : 0;

                var score = hintBoost + rect.Width - rect.Top * 5;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestValue = value;
                }
            }

            _lastUrlHwnd = hwnd;
            _lastTitle = currentTitle;
            _lastUrl = bestValue;
            _lastUrlDetectTime = now;

            return bestValue;
        }
        catch
        {
            return "";
        }
    }

    private static bool LooksLikeUrlOrDomain(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length < 4) return false;
        if (value.Contains("://", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("edge://", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Contains('.')) return true;
        return false;
    }
}

