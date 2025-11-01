using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Management;

#if ENABLE_SHARPDX
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using Device = SharpDX.Direct3D11.Device;
#endif
public sealed class AggressivePerformanceManager : IDisposable
{
    private const string GUID_HIGH_PERFORMANCE = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string GUID_BALANCED = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public TimeSpan MonitorInterval { get; set; } = TimeSpan.FromSeconds(3);
    public int CpuThresholdPercent { get; set; } = 50;
    public int GpuThresholdPercent { get; set; } = 60;
    public TimeSpan MinModeHoldTime { get; set; } = TimeSpan.FromSeconds(15);

    public bool RaiseProcessPriority { get; set; } = true;
    public bool SetAffinityToAllLogicalProcessors { get; set; } = true;
    public bool IncreaseTimerResolution { get; set; } = true;
    public bool UseGpuStress { get; set; } = true;
    public int GpuStressThreadsPerDispatch { get; set; } = 256;
    public int GpuStressWorkMultiplier { get; set; } = 8;

    private readonly string[] _targetProcessNames = new[] { "RobloxPlayerBeta", "RobloxPlayerLauncher", "Roblox", "Roblox Game" };
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private DateTime _lastModeChange = DateTime.MinValue;
    private bool _isInHighPerformanceMode = false;
    public event Action<string>? OnLog;
    public event Action<bool>? OnModeChanged;
    private const string LOG_IDENT = "AggressivePerf";
    private bool _timerIncreased = false;
#if ENABLE_SHARPDX
    private Device? _d3dDevice;
    private ComputeShader? _computeShader;
    private bool _gpuResourcesInitialized = false;
#endif

    public AggressivePerformanceManager()
    {
    }

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
        Log("AggressivePerformanceManager started.");
    }

    public void Stop()
    {
        if (_cts == null) return;

        _cts.Cancel();
        try
        {
            _monitorTask?.Wait(3000);
        }
        catch (AggregateException) { }
        catch (Exception ex)
        {
            Log($"Stop wait error: {ex.Message}");
        }
        TrySetBalancedPlan();
        TryNormalizeTargetPriorities();
        if (_timerIncreased)
        {
            TryRestoreTimerResolution();
            _timerIncreased = false;
        }

#if ENABLE_SHARPDX
        DisposeGpuResources();
#endif

        _isInHighPerformanceMode = false;
        _cts.Dispose();
        _cts = null;
        Log("AggressivePerformanceManager stopped and defaults restored.");
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var proc = GetTargetProcess();
                    if (proc == null)
                    {
                        if (_isInHighPerformanceMode && CooldownElapsed())
                        {
                            Log("Target process not found: switching to Balanced plan.");
                            TrySetBalancedPlan();
                            _isInHighPerformanceMode = false;
                            OnModeChanged?.Invoke(false);
                            _lastModeChange = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        int cpuPct = await SampleProcessCpuPercentAsync(proc, token).ConfigureAwait(false);
                        int gpuPct = TryGetGpuUsageForProcess(proc.Id);

                        Log($"Detected PID {proc.Id} CPU {cpuPct}% GPU {(gpuPct >= 0 ? gpuPct.ToString() + "%" : "N/A")}");

                        bool shouldHighPerf = cpuPct >= CpuThresholdPercent || (gpuPct >= 0 && gpuPct >= GpuThresholdPercent);

                        if (shouldHighPerf && !_isInHighPerformanceMode && CooldownElapsed())
                        {
                            Log("Aggressive trigger: switching to High Performance.");
                            if (TrySetHighPerformancePlan())
                            {
                                _isInHighPerformanceMode = true;
                                OnModeChanged?.Invoke(true);
                                Log("High Performance applied.");
                            }

                            if (IncreaseTimerResolution)
                            {
                                TryIncreaseTimerResolution();
                                _timerIncreased = true;
                            }

                            if (RaiseProcessPriority)
                            {
                                TryRaiseProcessPriority(proc, elevateToRealtime: false);
                            }

                            if (SetAffinityToAllLogicalProcessors)
                            {
                                TrySetProcessAffinity(proc, useAllLogicalProcessors: true);
                            }

#if ENABLE_SHARPDX
                            if (UseGpuStress)
                            {
                                await TryStartGpuStressAsync(token).ConfigureAwait(false);
                            }
#endif

                            _lastModeChange = DateTime.UtcNow;
                        }
                        else if (!shouldHighPerf && _isInHighPerformanceMode && CooldownElapsed())
                        {
                            Log("Load reduced: restoring Balanced plan and normal priorities.");
                            TrySetBalancedPlan();
                            _isInHighPerformanceMode = false;
                            OnModeChanged?.Invoke(false);
                            TryNormalizeTargetPriorities();
                            if (_timerIncreased)
                            {
                                TryRestoreTimerResolution();
                                _timerIncreased = false;
                            }

#if ENABLE_SHARPDX
                            DisposeGpuResources();
#endif

                            _lastModeChange = DateTime.UtcNow;
                        }
                        else
                        {
#if ENABLE_SHARPDX
                            if (UseGpuStress && _isInHighPerformanceMode && _gpuResourcesInitialized)
                            {
                                // continue dispatching more compute work to maintain high GPU usage
                                TryDispatchGpuWork(multiplier: GpuStressWorkMultiplier);
                            }
#endif
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Monitor iteration error: {ex.Message}");
                }

                await Task.Delay(MonitorInterval, token).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Log($"Monitor loop crashed: {ex}");
        }
    }

    private bool CooldownElapsed() => (DateTime.UtcNow - _lastModeChange) >= MinModeHoldTime;

    private Process? GetTargetProcess()
    {
        foreach (var name in _targetProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs != null && procs.Length > 0)
                {
                    var choose = procs.FirstOrDefault(p => !p.HasExited);
                    if (choose != null) return choose;
                }
            }
            catch { }
        }
        return null;
    }

    private async Task<int> SampleProcessCpuPercentAsync(Process proc, CancellationToken token)
    {
        try
        {
            proc.Refresh();
            if (proc.HasExited) return 0;

            TimeSpan t0 = proc.TotalProcessorTime;
            DateTime s0 = DateTime.UtcNow;
            await Task.Delay(500, token).ConfigureAwait(false);
            proc.Refresh();
            if (proc.HasExited) return 0;

            TimeSpan t1 = proc.TotalProcessorTime;
            DateTime s1 = DateTime.UtcNow;

            double usedMs = (t1 - t0).TotalMilliseconds;
            double elapsedMs = (s1 - s0).TotalMilliseconds;
            int logical = Environment.ProcessorCount;
            if (elapsedMs <= 0 || logical <= 0) return 0;
            double pct = (usedMs / (elapsedMs * logical)) * 100.0;
            Log($"Sampled CPU for PID {proc.Id}: {pct:0.##}%");
            return (int)Math.Round(Math.Max(0, Math.Min(100, pct)));
        }
        catch (Exception ex)
        {
            Log($"Sample CPU error: {ex.Message}");
            return 0;
        }
    }
    private int TryGetGpuUsageForProcess(int pid)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return -1;

            const string wmiClass = "Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine";
            using var searcher = new ManagementObjectSearcher($"SELECT Name, UtilizationPercentage FROM {wmiClass}");
            var results = searcher.Get();
            if (results == null || results.Count == 0) return -1;

            int total = 0, count = 0;
            foreach (ManagementObject mo in results)
            {
                var nameObj = mo["Name"] ?? mo["InstanceName"] ?? mo["Caption"];
                if (nameObj == null) continue;
                string name = nameObj.ToString();
                if (name.IndexOf($"pid_{pid}", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf($"pid:{pid}", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (int.TryParse(mo["UtilizationPercentage"]?.ToString() ?? "0", out int util))
                    {
                        total += util;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                int avg = (int)Math.Round((double)total / count);
                Log($"GPU usage for PID {pid}: {avg}%");
                return avg;
            }

            return -1;
        }
        catch (Exception ex)
        {
            Log($"GPU usage WMI error: {ex.Message}");
            return -1;
        }
    }

    #region Power plans (powercfg)
    private bool TrySetHighPerformancePlan()
    {
        try
        {
            var ok = TryRunPowerCfg($"/S {GUID_HIGH_PERFORMANCE}");
            Log(ok ? "High Performance plan set." : "Failed to set High Performance plan.");
            return ok;
        }
        catch (Exception ex)
        {
            Log($"SetHighPerformancePlan error: {ex.Message}");
            return false;
        }
    }

    private bool TrySetBalancedPlan()
    {
        try
        {
            var ok = TryRunPowerCfg($"/S {GUID_BALANCED}");
            Log(ok ? "Balanced plan set." : "Failed to set Balanced plan.");
            return ok;
        }
        catch (Exception ex)
        {
            Log($"SetBalancedPlan error: {ex.Message}");
            return false;
        }
    }

    private bool TryRunPowerCfg(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(4000);
            var outp = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(outp)) Log($"powercfg: {outp.Trim()}");
            if (!string.IsNullOrWhiteSpace(err)) Log($"powercfg err: {err.Trim()}");
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log($"powercfg invoke error: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Process priority & affinity

    private void TryRaiseProcessPriority(Process proc, bool elevateToRealtime = false)
    {
        try
        {
            if (proc == null || proc.HasExited) return;

            var desired = elevateToRealtime ? ProcessPriorityClass.RealTime : ProcessPriorityClass.High;
            foreach (var p in Process.GetProcessesByName(proc.ProcessName))
            {
                try
                {
                    if (p.HasExited) continue;
                    p.PriorityClass = desired;
                    Log($"Set process {p.Id} priority to {desired}.");
                }
                catch (Exception ex)
                {
                    Log($"Cannot set priority for PID {p.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"RaiseProcessPriority error: {ex.Message}");
        }
    }

    private void TryNormalizeTargetPriorities()
    {
        try
        {
            foreach (var name in _targetProcessNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (p.HasExited) continue;
                        p.PriorityClass = ProcessPriorityClass.Normal;
                        Log($"Restored PID {p.Id} priority to Normal.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Normalize priority error for PID {p.Id}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Normalize priorities error: {ex.Message}");
        }
    }

    private void TrySetProcessAffinity(Process proc, bool useAllLogicalProcessors = true)
    {
        try
        {
            if (proc == null || proc.HasExited) return;
            ulong mask = 0;
            if (useAllLogicalProcessors)
            {
                int logical = Environment.ProcessorCount;
                for (int i = 0; i < logical; i++)
                    mask |= (1UL << i);
            }
            else
            {
                int logical = Environment.ProcessorCount;
                for (int i = 0; i < logical; i++)
                {
                    if (i % 2 == 0) mask |= (1UL << i);
                }
            }
            if (IntPtr.Size == 4)
            {
                proc.ProcessorAffinity = new IntPtr((int)(mask & 0xFFFFFFFF));
            }
            else
            {
                proc.ProcessorAffinity = new IntPtr((long)mask);
            }

            Log($"Set PID {proc.Id} affinity to mask {mask:X}.");
        }
        catch (Exception ex)
        {
            Log($"Set affinity error: {ex.Message}");
        }
    }

    #endregion

    #region TimeBeginPeriod (increase timer resolution)
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uMilliseconds);
    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uMilliseconds);

    private void TryIncreaseTimerResolution()
    {
        try
        {
            uint res = 1;
            var rc = timeBeginPeriod(res);
            Log($"timeBeginPeriod({res}) returned {rc}.");
        }
        catch (Exception ex)
        {
            Log($"IncreaseTimerResolution error: {ex.Message}");
        }
    }

    private void TryRestoreTimerResolution()
    {
        try
        {
            uint res = 1;
            var rc = timeEndPeriod(res);
            Log($"timeEndPeriod({res}) returned {rc}.");
        }
        catch (Exception ex)
        {
            Log($"RestoreTimerResolution error: {ex.Message}");
        }
    }
    #endregion

    #region GPU stress (optional) - SharpDX-based compute work
#if ENABLE_SHARPDX
    private async Task TryStartGpuStressAsync(CancellationToken token)
    {
        try
        {
            if (!UseGpuStress) return;
            if (!_gpuResourcesInitialized)
            {
                InitializeGpuResources();
            }

            // run an initial set of dispatches to ramp GPU
            await Task.Run(() =>
            {
                TryDispatchGpuWork(multiplier: GpuStressWorkMultiplier);
            }, token).ConfigureAwait(false);

            Log("GPU stress started (compute dispatches).");
        }
        catch (Exception ex)
        {
            Log($"StartGpuStress error: {ex.Message}");
        }
    }

    private void InitializeGpuResources()
    {
        try
        {
            if (_gpuResourcesInitialized) return;

            // Create D3D11 device (hardware). Use BGRA support to be safe.
            _d3dDevice = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.None);

            // Very small HLSL compute shader that performs arithmetic loops to occupy ALUs / memory.
            string hlsl = @"
                RWStructuredBuffer<uint> buf : register(u0);

                [numthreads(256,1,1)]
                void CS(uint3 DTid : SV_DispatchThreadID)
                {
                    uint idx = DTid.x;
                    uint val = buf[idx];
                    // simple loop to burn cycles and memory
                    for (uint i=0;i<1024;i++)
                    {
                        val = (val * 1664525u) + 1013904223u;
                    }
                    buf[idx] = val;
                }
            ";

            // compile
            using (var bc = ShaderBytecode.Compile(hlsl, "CS", "cs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None))
            {
                _computeShader = new ComputeShader(_d3dDevice, bc);
            }

            // create a large structured buffer to be bound to u0
            int elementCount = 1024 * 256; // large buffer to occupy memory
            var bufferDesc = new SharpDX.Direct3D11.BufferDescription()
            {
                BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.BufferStructured,
                SizeInBytes = elementCount * sizeof(uint),
                StructureByteStride = sizeof(uint),
                Usage = ResourceUsage.Default
            };

            var initialData = new DataStream(elementCount * sizeof(uint), true, true);
            for (int i = 0; i < elementCount; i++) initialData.Write((uint)i);
            initialData.Position = 0;

            using (var initBox = new DataBox(initialData.DataPointer, 0, 0))
            {
                var buf = new SharpDX.Direct3D11.Buffer(_d3dDevice, initialData, bufferDesc);
                // create UAV
                var uavDesc = new UnorderedAccessViewDescription
                {
                    Dimension = UnorderedAccessViewDimension.Buffer,
                    Buffer = new UnorderedAccessViewDescription.BufferResource
                    {
                        FirstElement = 0,
                        ElementCount = elementCount
                    },
                    Format = SharpDX.DXGI.Format.Unknown
                };
                var uav = new UnorderedAccessView(_d3dDevice, buf, uavDesc);
                // bind UAV to slot 0 when dispatching
                // store buffers in device immediate context (we will set/unset on dispatch)
                _gpuResourcesInitialized = true;
                // keep references in compute shader class fields? we will store in device properties
                // To keep example concise: detach and recreate on each dispatch using local resources above or store fields.
                // For clarity in this example we keep compute shader and device.
            }

            Log("GPU resources initialized (D3D11 compute shader).");
        }
        catch (Exception ex)
        {
            Log($"InitializeGpuResources error: {ex.Message}");
            // release partial resources on error
            DisposeGpuResources();
        }
    }

    private void TryDispatchGpuWork(int multiplier = 1)
    {
        try
        {
            if (!_gpuResourcesInitialized || _d3dDevice == null || _computeShader == null) return;

            var ctx = _d3dDevice.ImmediateContext;

            // NOTE: in a production version you would keep buffers and UAVs as fields; here we create a moderate-sized buffer
            // to ensure work is dispatched. This simplified flow is for demonstration; optimize as needed.

            int elementCount = 1024 * 256;
            var bufferDesc = new SharpDX.Direct3D11.BufferDescription()
            {
                BindFlags = BindFlags.UnorderedAccess,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.BufferStructured,
                SizeInBytes = elementCount * sizeof(uint),
                StructureByteStride = sizeof(uint),
                Usage = ResourceUsage.Default
            };

            // create initial contents on CPU to upload once per dispatch block
            using (var ds = new DataStream(elementCount * sizeof(uint), true, true))
            {
                for (int i = 0; i < elementCount; i++) ds.Write((uint)i);
                ds.Position = 0;
                using (var buf = new SharpDX.Direct3D11.Buffer(_d3dDevice, ds, bufferDesc))
                {
                    var uavDesc = new UnorderedAccessViewDescription
                    {
                        Dimension = UnorderedAccessViewDimension.Buffer,
                        Buffer = new UnorderedAccessViewDescription.BufferResource
                        {
                            FirstElement = 0,
                            ElementCount = elementCount
                        },
                        Format = SharpDX.DXGI.Format.Unknown
                    };
                    using (var uav = new UnorderedAccessView(_d3dDevice, buf, uavDesc))
                    {
                        ctx.ComputeShader.Set(_computeShader);
                        ctx.ComputeShader.SetUnorderedAccessView(0, uav);

                        int threadsPerGroup = GpuStressThreadsPerDispatch; // 256 by default -> matches numthreads
                        int groups = Math.Max(1, elementCount / threadsPerGroup);

                        for (int i = 0; i < multiplier; i++)
                        {
                            ctx.Dispatch(groups, 1, 1);
                        }

                        // unbind
                        ctx.ComputeShader.SetUnorderedAccessView(0, null);
                        ctx.ComputeShader.Set(null);
                        ctx.Flush();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"DispatchGpuWork error: {ex.Message}");
        }
    }

    private void DisposeGpuResources()
    {
        try
        {
            if (_computeShader != null)
            {
                _computeShader.Dispose();
                _computeShader = null;
            }
            if (_d3dDevice != null)
            {
                _d3dDevice.Dispose();
                _d3dDevice = null;
            }
            _gpuResourcesInitialized = false;
            Log("GPU resources disposed.");
        }
        catch (Exception ex)
        {
            Log($"DisposeGpuResources error: {ex.Message}");
        }
    }
#endif
    #endregion

    #region Logging
    private void Log(string message)
    {
        try
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            var formatted = $"[{ts}] {message}";
            OnLog?.Invoke(formatted);
            Debug.WriteLine($"{LOG_IDENT}: {formatted}");
        }
        catch { }
    }
    #endregion

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
