using System;
using System.Diagnostics;

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
            if (coreCount < 1)
                coreCount = 1;

            int maxCores = Environment.ProcessorCount;
            if (coreCount > maxCores)
                coreCount = maxCores;

            // Create an affinity mask with coreCount bits set to 1
            IntPtr affinityMask = (IntPtr)((1 << coreCount) - 1);

            Process currentProcess = Process.GetCurrentProcess();
            try
            {
                currentProcess.ProcessorAffinity = affinityMask;
                Console.WriteLine($"CPU affinity set to {coreCount} core(s).");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to set CPU affinity: " + ex.Message);
            }
        }
    }
}
