using System;
using System.Windows.Forms;
using System.Drawing;

namespace Voidstrap.UI.Utility
{
    public static class WindowScaling
    {
        // Cache the scale factor once, assuming resolution doesn't change during runtime.
        private static readonly double _scaleFactor;

        static WindowScaling()
        {
            // Prevent division by zero, fallback to 1.0 if necessary.
            var screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            var sysScreenWidth = SystemInformation.PrimaryMonitorSize.Width;

            _scaleFactor = sysScreenWidth == 0 ? 1.0 : (double)screenWidth / sysScreenWidth;
        }

        public static double ScaleFactor => _scaleFactor;

        public static int GetScaledNumber(int number)
        {
            return (int)Math.Ceiling(number * _scaleFactor);
        }

        public static Size GetScaledSize(Size size)
        {
            return new Size(GetScaledNumber(size.Width), GetScaledNumber(size.Height));
        }

        public static Point GetScaledPoint(Point point)
        {
            return new Point(GetScaledNumber(point.X), GetScaledNumber(point.Y));
        }

        public static Padding GetScaledPadding(Padding padding)
        {
            return new Padding(
                GetScaledNumber(padding.Left),
                GetScaledNumber(padding.Top),
                GetScaledNumber(padding.Right),
                GetScaledNumber(padding.Bottom)
            );
        }
    }
}
