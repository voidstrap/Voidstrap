using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Wpf.Ui.Hardware
{
    public static class HardwareAcceleration
    {
        public enum RenderingTier
        {
            NoAcceleration = 0,
            PartialAcceleration = 1,
            FullAcceleration = 2
        }

        public static bool IsSupported(RenderingTier tier)
        {
            int currentTier = RenderCapability.Tier >> 16;
            return currentTier >= (int)tier;
        }

        public static bool AnimationsDisabled { get; set; } = false;

        public static void MinimizeMemoryFootprint()
        {
            DisableAllAnimations();
            OptimizeVisualRendering(disableTransparency: true, forceSoftwareRendering: true);
            FreeMemory();
            LowerProcessPriority();
            TrimWorkingSet();
        }

        public static void FreeMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

                TrimWorkingSet();
                NativeMethods.FlushProcessWriteBuffers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FreeMemory Error] {ex.Message}");
            }
        }

        public static void DisableAllAnimations()
        {
            if (AnimationsDisabled) return;

            try
            {
                Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(Timeline),
                    new FrameworkPropertyMetadata(3));

                SystemParameters.StaticPropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
                    {
                        typeof(SystemParameters).GetProperty(nameof(SystemParameters.ClientAreaAnimation))?
                            .SetValue(null, false);
                    }
                };

                AnimationsDisabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DisableAllAnimations Error] {ex.Message}");
            }
        }

        public static void OptimizeVisualRendering(bool disableTransparency = true, bool forceSoftwareRendering = false)
        {
            try
            {
                RenderOptions.ProcessRenderMode = forceSoftwareRendering
                    ? RenderMode.SoftwareOnly
                    : RenderMode.Default;

                TextOptions.TextFormattingModeProperty.OverrideMetadata(
                    typeof(DependencyObject),
                    new FrameworkPropertyMetadata(TextFormattingMode.Display));

                TextOptions.TextRenderingModeProperty.OverrideMetadata(
                    typeof(DependencyObject),
                    new FrameworkPropertyMetadata(TextRenderingMode.Aliased));

                if (disableTransparency)
                    DisableTransparencyEffects();

                DisableAnimationsOSLevel();
                DisableHardwareAccelerationInWpf();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OptimizeVisualRendering Error] {ex.Message}");
            }
        }

        public static void DisableTransparencyEffects()
        {
            try
            {
                const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
                bool disable = true;

                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow == null || !mainWindow.IsLoaded) return;

                var hwnd = new WindowInteropHelper(mainWindow).Handle;
                if (hwnd == IntPtr.Zero) return;

                NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disable, Marshal.SizeOf(disable));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DisableTransparencyEffects Error] {ex.Message}");
            }
        }

        private static void DisableAnimationsOSLevel()
        {
            try
            {
                SystemParametersInfo(SPI_SETCLIENTAREAANIMATION, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                SystemParametersInfo(SPI_SETCOMBOBOXANIMATION, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                SystemParametersInfo(SPI_SETLISTBOXSMOOTHSCROLLING, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                SystemParametersInfo(SPI_SETMENUANIMATION, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                SystemParametersInfo(SPI_SETOBJECTANIMATION, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                SystemParametersInfo(SPI_SETTOOLTIPANIMATION, 0, IntPtr.Zero, SPIF_SENDCHANGE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DisableAnimationsOSLevel Error] {ex.Message}");
            }
        }

        private static void DisableHardwareAccelerationInWpf()
        {
            try
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DisableHardwareAccelerationInWpf Error] {ex.Message}");
            }
        }

        public static void LowerProcessPriority()
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LowerProcessPriority Error] {ex.Message}");
            }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                IntPtr processHandle = Process.GetCurrentProcess().Handle;
                NativeMethods.SetProcessWorkingSetSize(processHandle, -1, -1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrimWorkingSet Error] {ex.Message}");
            }
        }

        private const uint SPI_SETCLIENTAREAANIMATION = 0x1042;
        private const uint SPI_SETCOMBOBOXANIMATION = 0x1005;
        private const uint SPI_SETLISTBOXSMOOTHSCROLLING = 0x1007;
        private const uint SPI_SETMENUANIMATION = 0x1002;
        private const uint SPI_SETOBJECTANIMATION = 0x1009;
        private const uint SPI_SETTOOLTIPANIMATION = 0x1017;
        private const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);

            [DllImport("kernel32.dll")]
            public static extern void FlushProcessWriteBuffers();

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref bool attrValue, int attrSize);
        }
    }
}
