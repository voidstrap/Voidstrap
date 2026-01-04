using System;
using System.Runtime.InteropServices;
using System.Windows;
using static Voidstrap.Models.Persistable.AppSettings;

namespace Voidstrap.Integrations
{
    public static class InGameResolutionApplier
    {
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int DISP_CHANGE_SUCCESSFUL = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        public static void Apply(ResolutionSetting res)
        {
            DEVMODE dm = new();
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
                return;

            dm.dmPelsWidth = (uint)res.Width;
            dm.dmPelsHeight = (uint)res.Height;
            dm.dmDisplayFrequency = (uint)res.RefreshRate;
            dm.dmBitsPerPel = 32;
            dm.dmFields = 0x180000 | 0x400000;

            int result = ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);

            if (result != DISP_CHANGE_SUCCESSFUL)
            {
                App.Logger.WriteLine(
                    "InGameResolution",
                    $"Failed to apply in-game resolution ({res.Width}x{res.Height}@{res.RefreshRate})"
                );
            }
        }
    }
}
