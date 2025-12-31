using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public static class RobloxFullscreen
{
    private const int GWL_STYLE = -16;

    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;
    private const uint WS_MINIMIZE = 0x20000000;
    private const uint WS_MAXIMIZE = 0x01000000;
    private const uint WS_SYSMENU = 0x00080000;

    private const uint SWP_NOZORDER = 0x0004; // whys this needed again gulp ANT!!
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public static void WaitAndForceBorderlessFullscreen()
    {
        const string LOG = "RobloxFullscreen";
        string processName = Voidstrap.App.RobloxPlayerAppName.Split('.')[0];

        Voidstrap.App.Logger.WriteLine(LOG, $"Waiting for {processName} window…");

        var sw = Stopwatch.StartNew();

        while (sw.Elapsed.TotalSeconds < 60)
        {
            var roblox = Process.GetProcessesByName(processName)
                .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

            if (roblox != null)
            {
                ForceBorderlessFullscreen(roblox.MainWindowHandle);
                return;
            }

            Thread.Sleep(400);
        }

        Voidstrap.App.Logger.WriteLine(LOG, "Timed out waiting for Roblox window");
    }

    private static void ForceBorderlessFullscreen(IntPtr hwnd)
    {
        const string LOG = "RobloxFullscreen";

        var screen = Screen.FromHandle(hwnd).Bounds;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed.TotalSeconds < 15)
        {
            SetForegroundWindow(hwnd);

            int style = GetWindowLong(hwnd, GWL_STYLE);

            style &= unchecked((int)~(
                WS_CAPTION |
                WS_THICKFRAME |
                WS_MINIMIZE |
                WS_MAXIMIZE |
                WS_SYSMENU));

            SetWindowLong(hwnd, GWL_STYLE, style);

            SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                screen.X,
                screen.Y,
                screen.Width,
                screen.Height,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW);

            Thread.Sleep(250);

            if (IsFullscreen(hwnd, screen))
            {
                Voidstrap.App.Logger.WriteLine(LOG, "Borderless fullscreen confirmed");
                return;
            }
        }

        Voidstrap.App.Logger.WriteLine(LOG, "Failed to enforce fullscreen");
    }

    private static bool IsFullscreen(IntPtr hwnd, System.Drawing.Rectangle screen)
    {
        if (!GetWindowRect(hwnd, out RECT r))
            return false;

        int width = r.Right - r.Left;
        int height = r.Bottom - r.Top;

        return Math.Abs(width - screen.Width) <= 5 &&
               Math.Abs(height - screen.Height) <= 5;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
