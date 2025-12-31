using System.Windows.Media.Imaging;
using System.Windows.Media;
using System;

namespace RobloxLightingOverlay.Effects
{
    public sealed class CameraMotionDetector
    {
        private WriteableBitmap _last;
        private double _dx;
        private double _dy;

        public double DirectionX => _dx;
        public double DirectionY => _dy;
        public double Strength { get; private set; }

        public unsafe void Analyze(WriteableBitmap current)
        {
            if (current == null || current.Format != PixelFormats.Bgra32)
                return;

            if (_last == null ||
                _last.PixelWidth != current.PixelWidth ||
                _last.PixelHeight != current.PixelHeight)
            {
                _last = Clone(current);
                return;
            }

            current.Lock();
            _last.Lock();

            byte* cur = (byte*)current.BackBuffer;
            byte* last = (byte*)_last.BackBuffer;

            long motionX = 0;
            long motionY = 0;
            int samples = 0;

            int stride = current.BackBufferStride;

            for (int y = 0; y < current.PixelHeight; y += 12)
            {
                for (int x = 0; x < current.PixelWidth * 4; x += 48)
                {
                    int idx = y * stride + x;

                    int d =
                        Math.Abs(cur[idx] - last[idx]) +
                        Math.Abs(cur[idx + 1] - last[idx + 1]) +
                        Math.Abs(cur[idx + 2] - last[idx + 2]);

                    if (d > 40)
                    {
                        motionX += x;
                        motionY += y;
                        samples++;
                    }
                }
            }

            if (samples > 0)
            {
                _dx = (motionX / (double)samples) - current.PixelWidth / 2.0;
                _dy = (motionY / (double)samples) - current.PixelHeight / 2.0;
                Strength = Math.Clamp(samples / 1200.0, 0.05, 1.0);
            }
            else
            {
                Strength *= 0.9;
            }

            Buffer.MemoryCopy(
                cur, last,
                stride * current.PixelHeight,
                stride * current.PixelHeight);

            _last.Unlock();
            current.Unlock();
        }

        public void Reset()
        {
            _last = null;
            _dx = _dy = Strength = 0;
        }

        private static WriteableBitmap Clone(WriteableBitmap src)
        {
            var wb = new WriteableBitmap(
                src.PixelWidth,
                src.PixelHeight,
                src.DpiX,
                src.DpiY,
                src.Format,
                null);

            src.Lock();
            wb.Lock();

            unsafe
            {
                Buffer.MemoryCopy(
                    src.BackBuffer.ToPointer(),
                    wb.BackBuffer.ToPointer(),
                    wb.BackBufferStride * wb.PixelHeight,
                    wb.BackBufferStride * wb.PixelHeight);
            }

            wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
            src.Unlock();

            return wb;
        }
    }
}
