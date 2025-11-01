using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RobloxDX12Optimizer
{
    public static class RobloxDx12Optimizer
    {
        private static CancellationTokenSource? _cts;
        private static Task? _backgroundTask;
        private static readonly string[] RobloxProcessNames = new[]
        {
            "RobloxPlayerBeta",
            "RobloxPlayerBeta.exe",
            "RobloxPlayer",
            "Roblox",
            "RobloxGame",
            "RobloxStudio",
            "RobloxMainGame",
            "Roblox Client"
        };
        public static void Start(int intervalMs = 2000, OptimizerOptions? options = null)
        {
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
            {
                Logger.Info("Optimizer already running.");
                return;
            }

            options ??= new OptimizerOptions();

            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(() => LoopAsync(intervalMs, options, _cts.Token), _cts.Token);
            Logger.Info("Optimizer started.");
        }
        public static async Task StopAsync()
        {
            if (_cts == null)
            {
                Logger.Info("Optimizer not running.");
                return;
            }

            try
            {
                _cts.Cancel();
                if (_backgroundTask != null)
                    await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Warn("Error while stopping optimizer: " + ex.Message);
            }
            finally
            {
                if (_lastRequestedTimerPeriod > 0)
                {
                    try
                    {
                        timeEndPeriod(_lastRequestedTimerPeriod);
                        Logger.Info($"timeEndPeriod({_lastRequestedTimerPeriod}) called on stop.");
                        _lastRequestedTimerPeriod = 0;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("timeEndPeriod failed: " + ex.Message);
                    }
                }

                _cts.Dispose();
                _cts = null;
                _backgroundTask = null;
                Logger.Info("Optimizer stopped.");
            }
        }

        public sealed class OptimizerOptions
        {
            public bool EnableHighResTimer { get; set; } = true;
            public uint HighResMillis { get; set; } = 1;
            public bool OverrideAffinity { get; set; } = false;
            public ulong AffinityMask { get; set; } = ulong.MaxValue;
            public ProcessPriorityClass PriorityClass { get; set; } = ProcessPriorityClass.High;
            public int WorkingSetMinMB { get; set; } = 64;
            public int WorkingSetMaxMB { get; set; } = 1024;
            public bool BoostThreads { get; set; } = true;
            public bool ProbeVendorAndDx { get; set; } = true;
            public bool SafeMode { get; set; } = true;
        }
        private static uint _lastRequestedTimerPeriod = 0;

        private static async Task LoopAsync(int intervalMs, OptimizerOptions options, CancellationToken token)
        {
            if (intervalMs < 200) intervalMs = 200;

            if (options.EnableHighResTimer && options.HighResMillis > 0)
            {
                try
                {
                    var ret = timeBeginPeriod(options.HighResMillis);
                    _lastRequestedTimerPeriod = options.HighResMillis;
                    Logger.Info($"timeBeginPeriod({options.HighResMillis}) -> {ret}");
                }
                catch (Exception ex)
                {
                    Logger.Warn("timeBeginPeriod failed: " + ex.Message);
                }
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var procs = FindRobloxProcesses();
                    if (procs.Length == 0)
                    {
                        Logger.Info("No Roblox processes found on this pass.");
                    }

                    foreach (var proc in procs)
                    {
                        try
                        {
                            proc.Refresh();
                            if (proc.HasExited)
                                continue;

                            Logger.Info($"Optimizing PID {proc.Id} ({proc.ProcessName})");

                            ApplyPerProcessTweaks(proc, options);

                            if (options.ProbeVendorAndDx)
                            {
                                ProbeAndInitVendorApisSafe();
                                ProbeForDx12UsageSafe(proc);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Error optimizing PID {proc.Id}: {ex.Message}");
                        }
                        finally
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Main loop error: " + ex.Message);
                }

                try
                {
                    await Task.Delay(intervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private static Process[] FindRobloxProcesses()
        {
            var list = new List<Process>();
            foreach (var name in RobloxProcessNames)
            {
                try
                {
                    var ps = Process.GetProcessesByName(name);
                    if (ps?.Length > 0) list.AddRange(ps);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed enumerating processes by name '{name}': {ex.Message}");
                }
            }
            try
            {
                var all = Process.GetProcesses();
                foreach (var p in all)
                {
                    try
                    {
                        if (!p.HasExited && p.ProcessName.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!list.Any(existing => existing.Id == p.Id))
                                list.Add(p);
                        }
                    }
                    catch {}
                }
            }
            catch {}
            return list.Where(p =>
            {
                try { p.Refresh(); return !p.HasExited; }
                catch { return false; }
            }).GroupBy(p => p.Id).Select(g => g.First()).ToArray();
        }

        private static void ApplyPerProcessTweaks(Process proc, OptimizerOptions options)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Refresh();
                    if (proc.PriorityClass != options.PriorityClass)
                    {
                        proc.PriorityClass = options.PriorityClass;
                        Logger.Info($"Set PID {proc.Id} PriorityClass to {options.PriorityClass}.");
                    }
                    else
                    {
                        Logger.Info($"PID {proc.Id} PriorityClass already {options.PriorityClass}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Setting process PriorityClass failed: " + ex.Message);
            }

            if (options.OverrideAffinity)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        long mask = (long)(options.AffinityMask & (ulong)IntPtr.MaxValue);
                        proc.ProcessorAffinity = new IntPtr(mask);
                        Logger.Info($"PID {proc.Id} ProcessorAffinity set to 0x{options.AffinityMask:X}.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Setting ProcessorAffinity failed (maybe insufficient privilege): " + ex.Message);
                }
            }

            try
            {
                if (!proc.HasExited)
                {
                    long minWs = Math.Max(4L * 1024 * 1024, (long)options.WorkingSetMinMB * 1024 * 1024);
                    long maxWs = Math.Max(minWs, (long)options.WorkingSetMaxMB * 1024 * 1024);
                    bool ok = SetProcessWorkingSetSize(proc.Handle, (IntPtr)minWs, (IntPtr)maxWs);
                    Logger.Info($"SetProcessWorkingSetSize(PID {proc.Id}, min={minWs}, max={maxWs}) -> {ok}");
                    bool emptied = EmptyWorkingSet(proc.Handle);
                    Logger.Info($"EmptyWorkingSet(PID {proc.Id}) -> {emptied}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Working set tweak failed: " + ex.Message);
            }

            if (options.BoostThreads)
            {
                try
                {
                    foreach (ProcessThread t in proc.Threads)
                    {
                        try
                        {
                            if (t.PriorityLevel == ThreadPriorityLevel.Normal)
                                t.PriorityLevel = ThreadPriorityLevel.AboveNormal;
                        }
                        catch { }
                    }
                    Logger.Info($"Attempted thread priority boost for PID {proc.Id}");
                }
                catch (Exception ex)
                {
                    Logger.Warn("Thread boosting error: " + ex.Message);
                }
            }
        }

        private static void ProbeAndInitVendorApisSafe()
        {
            Task.Run(() =>
            {
                try
                {
                    var nv = TryLoadNvApi();
                    Logger.Info("NVAPI probe: " + nv);
                }
                catch (Exception ex)
                {
                    Logger.Warn("NVAPI probe threw: " + ex.Message);
                }

                try
                {
                    var amd = TryLoadAmdAgs();
                    Logger.Info("AMD AGS probe: " + amd);
                }
                catch (Exception ex)
                {
                    Logger.Warn("AMD AGS probe threw: " + ex.Message);
                }
            });
        }
        private static void ProbeForDx12UsageSafe(Process proc)
        {
            Task.Run(() =>
            {
                try
                {
                    IntPtr h = GetModuleHandle("d3d12.dll");
                    if (h != IntPtr.Zero)
                    {
                        Logger.Info($"Local process can access d3d12.dll (module handle {h}).");
                        IntPtr fn = GetProcAddress(h, "D3D12CreateDevice");
                        if (fn != IntPtr.Zero)
                        {
                            Logger.Info("D3D12CreateDevice present in system d3d12.dll.");
                        }
                        else
                        {
                            Logger.Info("d3d12.dll present but D3D12CreateDevice not found locally.");
                        }
                    }
                    else
                    {
                        Logger.Info("d3d12.dll not present in local process environment (may still be loaded by game).");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("DX12 probe failed: " + ex.Message);
                }
            });
        }

        #region Vendor probing (NVAPI / AMD AGS)

        private static string TryLoadNvApi()
        {
            string[] nvLibs = { "nvapi64.dll", "nvapi.dll" };
            foreach (var lib in nvLibs)
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    handle = LoadLibrary(lib);
                }
                catch { handle = IntPtr.Zero; }

                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr fn = GetProcAddress(handle, "NvAPI_Initialize");
                        if (fn != IntPtr.Zero)
                        {
                            var init = Marshal.GetDelegateForFunctionPointer<NvApi_InitDelegate>(fn);
                            int ret = init();
                            return $"Loaded {lib}, NvAPI_Initialize returned 0x{ret:X}";
                        }
                        else
                        {
                            return $"Loaded {lib} but NvAPI_Initialize not found.";
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"Loaded {lib} but calling NvAPI_Initialize failed: {ex.Message}";
                    }
                }
            }
            return "NVAPI not found on system.";
        }

        private static string TryLoadAmdAgs()
        {
            string[] amdLibs = { "amd_ags_x64.dll", "amd_ags.dll" };
            foreach (var lib in amdLibs)
            {
                IntPtr handle = IntPtr.Zero;
                try { handle = LoadLibrary(lib); } catch { handle = IntPtr.Zero; }

                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr fn = GetProcAddress(handle, "agsInit");
                        if (fn == IntPtr.Zero)
                            fn = GetProcAddress(handle, "agsInitEx");

                        if (fn != IntPtr.Zero)
                        {
                            var init = Marshal.GetDelegateForFunctionPointer<AmdAgs_InitDelegate>(fn);
                            int ret = init();
                            return $"Loaded {lib}, agsInit returned 0x{ret:X}";
                        }
                        else
                        {
                            return $"Loaded {lib} but agsInit / agsInitEx not found.";
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"Loaded {lib} but calling agsInit failed: {ex.Message}";
                    }
                }
            }
            return "AMD AGS not found on system.";
        }

        #endregion

        #region P/Invoke & delegates

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvApi_InitDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AmdAgs_InitDelegate();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint uMilliseconds);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        #region Utilities & logging

        private static void LoggerInfoIfElevated()
        {
            // Example helper (not used currently) to log if elevated; left for future expansion.
        }

        private static void ProbeAndInitVendorApis() => ProbeAndInitVendorApisSafe(); // legacy compat

        private static void ProbeForDx12UsageSafe() => ProbeForDx12UsageSafe(Process.GetCurrentProcess());

        #endregion

        #region Simple logger

        private static class Logger
        {
            public static void Info(string s) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {s}");
            public static void Warn(string s) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} {s}");
            public static void Error(string s) => Console.WriteLine($"[ERR ] {DateTime.Now:HH:mm:ss} {s}");
        }

        #endregion
    }
}
