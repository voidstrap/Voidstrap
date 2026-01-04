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

    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const byte VK_MENU = 0x12;   // Alt
    private const byte VK_RETURN = 0x0D; // Enter
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

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
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public static void WaitAndForceExclusiveFullscreen()
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
                ApplyHybridFullscreen(roblox.MainWindowHandle);
                return;
            }

            Thread.Sleep(400);
        }

        Voidstrap.App.Logger.WriteLine(LOG, "Timed out waiting for Roblox window");
    }

    private static void ApplyHybridFullscreen(IntPtr hwnd)
    {
        const string LOG = "RobloxFullscreen";

        var screen = Screen.FromHandle(hwnd).Bounds;

        SetForegroundWindow(hwnd);
        Thread.Sleep(150);

        SendAltEnter();

        Thread.Sleep(800);

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

        if (IsFullscreen(hwnd, screen))
            Voidstrap.App.Logger.WriteLine(LOG, "Exclusive fullscreen + styles enforced");
        else
            Voidstrap.App.Logger.WriteLine(LOG, "Fullscreen attempt completed (verification failed)");
    }

    private static void SendAltEnter()
    {
        INPUT[] inputs =
        {
            KeyDown(VK_MENU),
            KeyDown(VK_RETURN),
            KeyUp(VK_RETURN),
            KeyUp(VK_MENU)
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static INPUT KeyDown(byte vk) => new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk }
        }
    };

    private static INPUT KeyUp(byte vk) => new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP }
        }
    };

    private static bool IsFullscreen(IntPtr hwnd, System.Drawing.Rectangle screen)
    {
        if (!GetWindowRect(hwnd, out RECT r))
            return false;

        return Math.Abs((r.Right - r.Left) - screen.Width) <= 5 &&
               Math.Abs((r.Bottom - r.Top) - screen.Height) <= 5;
    }

    #region Native structs

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    #endregion
}