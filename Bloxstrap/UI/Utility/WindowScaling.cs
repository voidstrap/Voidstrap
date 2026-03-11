using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Voidstrap.UI.Utility
{
    public static class WindowScaling
    {
        private static double _scaleFactor = 1.0;

        static WindowScaling()
        {
            RecalculateScaleFactor();
            SystemEvents.DisplaySettingsChanged += (s, e) => RecalculateScaleFactor();

            if (Environment.OSVersion.Version.Major >= 10)
            {
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.Window)
                        RecalculateScaleFactor();
                };
            }
        }
        public static void RecalculateScaleFactor()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    _scaleFactor = g.DpiX / 96.0;
                }
            }
            catch
            {
                _scaleFactor = 1.0;
            }
        }

        public static double ScaleFactor => _scaleFactor;
        public static int GetScaledValue(int value) => (int)Math.Round(value * _scaleFactor);
        public static Size GetScaledSize(Size size) =>
            new Size(GetScaledValue(size.Width), GetScaledValue(size.Height));
        public static Point GetScaledPoint(Point point) =>
            new Point(GetScaledValue(point.X), GetScaledValue(point.Y));
        public static Padding GetScaledPadding(Padding padding) =>
            new Padding(
                GetScaledValue(padding.Left),
                GetScaledValue(padding.Top),
                GetScaledValue(padding.Right),
                GetScaledValue(padding.Bottom));
        public static Rectangle GetScaledRectangle(Rectangle rect) =>
            new Rectangle(
                GetScaledValue(rect.X),
                GetScaledValue(rect.Y),
                GetScaledValue(rect.Width),
                GetScaledValue(rect.Height));
    }
}