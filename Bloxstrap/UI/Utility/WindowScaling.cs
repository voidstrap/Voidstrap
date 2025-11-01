using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Voidstrap.UI.Utility
{
    public static class WindowScaling
    {
        private static double _scaleFactor;

        static WindowScaling()
        {
            RecalculateScaleFactor();
            SystemEvents.DisplaySettingsChanged += (s, e) => RecalculateScaleFactor();
        }

        public static void RecalculateScaleFactor()
        {
            int screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            int sysScreenWidth = SystemInformation.PrimaryMonitorSize.Width;

            _scaleFactor = sysScreenWidth == 0 ? 1.0 : (double)screenWidth / sysScreenWidth;
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
