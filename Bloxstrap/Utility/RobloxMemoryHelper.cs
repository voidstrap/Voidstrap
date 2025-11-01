using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class RobloxMemoryCleaner
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private static readonly string[] RobloxProcesses =
    {
        "RobloxPlayerBeta",
        "RobloxPlayer",
        "Roblox",
        "RobloxStudioBeta"
    };
    public static void CleanAllRobloxMemory()
    {
        int totalTrimmed = 0;
        foreach (string procName in RobloxProcesses)
        {
            Process[] procs = null;
            try
            {
                procs = Process.GetProcessesByName(procName);
                foreach (var proc in procs)
                {
                    try
                    {
                        long before = proc.WorkingSet64;
                        if (EmptyWorkingSet(proc.Handle))
                        {
                            proc.Refresh();
                            long after = proc.WorkingSet64;
                            double pct = before > 0 ? 100.0 * (before - after) / before : 0;
                            Console.WriteLine($"[{proc.ProcessName}:{proc.Id}] Trimmed {pct:F1}% ({FormatBytes(before)} → {FormatBytes(after)})");
                            totalTrimmed++;
                        }
                    }
                    catch {}
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }
            }
            catch {}
            finally
            {
                if (procs != null)
                {
                    foreach (var p in procs)
                        try { p.Dispose(); } catch { }
                }
            }
        }

        if (totalTrimmed == 0)
            Console.WriteLine("No Roblox processes were trimmed.");
        else
            Console.WriteLine($"✔ Trimmed memory for {totalTrimmed} Roblox process(es).");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F2} GB";
    }
}
