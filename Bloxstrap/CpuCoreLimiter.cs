using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Voidstrap
{
    public static class CpuCoreLimiter
    {
        /// <summary>
        /// Limits the current process to use only the specified number of CPU cores.
        /// </summary>
        /// <param name="coreCount">Number of CPU cores to allow (minimum 1, maximum number of logical processors).</param>
        public static void SetCpuCoreLimit(int coreCount)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("CPU affinity setting is only supported on Windows.");
                return;
            }

            int maxCores = Environment.ProcessorCount;
            if (coreCount < 1)
                coreCount = 1;
            else if (coreCount > maxCores)
                coreCount = maxCores;

            try
            {
                long affinityMask = 0;
                for (int i = 0; i < coreCount; i++)
                {
                    affinityMask |= 1L << i;
                }

                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.ProcessorAffinity = (IntPtr)affinityMask;

                Console.WriteLine($"CPU affinity successfully set to {coreCount} core(s).");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to set CPU affinity.");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
