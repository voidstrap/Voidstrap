using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

public class RobloxFullscreen
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);


    private const byte VK_MENU = 0x12;
    private const byte VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void WaitAndTriggerFullscreen()
    {
        const string LOG_IDENT = "RobloxFullscreen::WaitAndTriggerAltEnter";

        string processName = Voidstrap.App.RobloxPlayerAppName.Split('.')[0];
        Voidstrap.App.Logger.WriteLine(LOG_IDENT, $"Waiting for {processName} to start and become visible...");

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 60)
        {
            Process[] processes = Process.GetProcessesByName(processName);

            if (processes.Length > 0)
            {
                Process roblox = processes[0];
                roblox.Refresh();

                if (roblox.MainWindowHandle != IntPtr.Zero && IsWindowVisible(roblox.MainWindowHandle))
                {
                    Voidstrap.App.Logger.WriteLine(LOG_IDENT, "Found visible Roblox window, triggering Alt+Enter");

                    SetForegroundWindow(roblox.MainWindowHandle);
                    Thread.Sleep(500);

                    SendAltEnter();
                    Voidstrap.App.Logger.WriteLine(LOG_IDENT, "Alt+Enter triggered");
                    return;
                }
            }

            Thread.Sleep(500);
        }

        Voidstrap.App.Logger.WriteLine(LOG_IDENT, "Timed out waiting for Roblox window");
    }

    private static void SendAltEnter()
    {
        keybd_event(VK_MENU, 0, 0, 0);

        keybd_event(VK_RETURN, 0, 0, 0);

        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);

        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
    }
}