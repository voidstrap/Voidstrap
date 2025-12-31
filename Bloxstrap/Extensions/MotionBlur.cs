using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfImage = System.Windows.Controls.Image;
using WpfBrushes = System.Windows.Media.Brushes;

namespace RobloxLightingOverlay
{
    public sealed class MotionBlurOverlay
    {
        private OverlayWindow _window;

        public void Start()
        {
            if (_window != null)
                return;

            _window = new OverlayWindow();
            _window.Start();
        }

        public void Stop()
        {
            _window?.Close();
            _window = null;
        }
    }

    internal sealed class OverlayWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly WpfImage _image;

        private Process _roblox;
        private TemporalSmoother _smoother;

        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = WpfBrushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;

            _image = new WpfImage
            {
                Stretch = Stretch.Fill,
                Opacity = 0.45,
                IsHitTestVisible = false
            };

            Content = _image;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            _roblox = RobloxHelper.FindRoblox();
            if (_roblox == null)
                return;

            MotionBlurManager.Start();
            _smoother = new TemporalSmoother(0.78f);

            Show();
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_roblox == null || _roblox.HasExited)
            {
                MotionBlurManager.Stop();
                Close();
                return;
            }

            RobloxHelper.GetWindowRect(_roblox.MainWindowHandle, out var r);

            Left = r.Left;
            Top = r.Top;
            Width = r.Right - r.Left;
            Height = r.Bottom - r.Top;

            var frame = ScreenCapture.Capture(
                r.Left, r.Top, (int)Width, (int)Height);

            MotionBlurManager.Apply(frame);
            frame = _smoother.Smooth(frame);

            _image.Source = frame;
        }
    }

    internal static class RobloxHelper
    {
        public static Process FindRoblox()
        {
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Equals(
                    "RobloxPlayerBeta",
                    StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(
            IntPtr hWnd,
            out RECT lpRect);

        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
    }

    internal static class ScreenCapture
    {
        public static WriteableBitmap Capture(
            int x, int y, int width, int height)
        {
            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);

            g.CopyFromScreen(x, y, 0, 0, bmp.Size);

            var hBitmap = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                ) as WriteableBitmap;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }

    internal sealed class TemporalSmoother
    {
        private WriteableBitmap _previous;
        private readonly float _alpha;

        public TemporalSmoother(float alpha)
        {
            _alpha = Math.Clamp(alpha, 0.1f, 0.95f);
        }

        public WriteableBitmap Smooth(WriteableBitmap current)
        {
            if (current == null)
                return null;

            if (_previous == null)
            {
                _previous = new WriteableBitmap(current);
                return current;
            }

            Blend(current, _previous, _alpha);
            _previous = new WriteableBitmap(current);
            return current;
        }

        private unsafe void Blend(
            WriteableBitmap cur,
            WriteableBitmap prev,
            float alpha)
        {
            cur.Lock();
            prev.Lock();

            int bytes = cur.PixelHeight * cur.BackBufferStride;
            byte* c = (byte*)cur.BackBuffer;
            byte* p = (byte*)prev.BackBuffer;

            for (int i = 0; i < bytes; i++)
                c[i] = (byte)(c[i] * (1f - alpha) + p[i] * alpha);

            cur.AddDirtyRect(
                new Int32Rect(0, 0, cur.PixelWidth, cur.PixelHeight));

            prev.Unlock();
            cur.Unlock();
        }
    }

    internal static class MotionBlurManager
    {
        private static MotionBlurEffect _blur;
        private static CameraMotionDetector _camera;

        public static bool IsEnabled => _blur != null;

        public static void Start()
        {
            if (IsEnabled)
                return;

            _blur = new MotionBlurEffect();
            _camera = new CameraMotionDetector();
        }

        public static void Apply(WriteableBitmap frame)
        {
            if (!IsEnabled || frame == null)
                return;

            _camera.Analyze(frame);
            _blur.Apply(
                frame,
                _camera.DirectionX,
                _camera.DirectionY,
                _camera.Strength);
        }

        public static void Stop()
        {
            _blur?.Dispose();
            _blur = null;
            _camera = null;
        }
    }

    internal sealed class MotionBlurEffect : IDisposable
    {
        public void Apply(
            WriteableBitmap bmp,
            float dx,
            float dy,
            float strength)
        {
        }

        public void Dispose() { }
    }
    internal sealed class CameraMotionDetector
    {
        public float DirectionX { get; private set; }
        public float DirectionY { get; private set; }
        public float Strength { get; private set; }

        public void Analyze(WriteableBitmap bmp)
        {
            DirectionX = 0.2f;
            DirectionY = 0.1f;
            Strength = 1.0f;
        }
    }
}
