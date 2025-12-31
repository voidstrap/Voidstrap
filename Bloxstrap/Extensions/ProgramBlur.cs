using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RobloxLightingOverlay.Effects
{
    public sealed class MotionBlurEffect : IDisposable
    {
        private WriteableBitmap _history;

        public unsafe void Apply(
            WriteableBitmap frame,
            double dirX,
            double dirY,
            double strength)
        {
            if (frame == null || frame.Format != PixelFormats.Bgra32)
                return;

            int w = frame.PixelWidth;
            int h = frame.PixelHeight;
            int stride = frame.BackBufferStride;

            if (_history == null ||
                _history.PixelWidth != w ||
                _history.PixelHeight != h)
            {
                _history = Clone(frame);
                return;
            }

            frame.Lock();
            _history.Lock();

            byte* cur = (byte*)frame.BackBuffer;
            byte* hist = (byte*)_history.BackBuffer;

            double keep = Math.Clamp(1.0 - strength, 0.1, 0.9);

            for (int y = 0; y < h; y++)
            {
                byte* cRow = cur + y * stride;
                byte* hRow = hist + y * stride;

                for (int x = 0; x < w * 4; x += 4)
                {
                    cRow[x] = (byte)(hRow[x] * keep + cRow[x] * (1 - keep));
                    cRow[x + 1] = (byte)(hRow[x + 1] * keep + cRow[x + 1] * (1 - keep));
                    cRow[x + 2] = (byte)(hRow[x + 2] * keep + cRow[x + 2] * (1 - keep));
                    cRow[x + 3] = 255;
                }
            }

            Buffer.MemoryCopy(
                cur, hist,
                stride * h,
                stride * h);

            frame.AddDirtyRect(new Int32Rect(0, 0, w, h));

            _history.Unlock();
            frame.Unlock();
        }

        private static WriteableBitmap Clone(WriteableBitmap src)
        {
            var wb = new WriteableBitmap(
                src.PixelWidth, src.PixelHeight,
                src.DpiX, src.DpiY,
                src.Format, null);

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

            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
            src.Unlock();

            return wb;
        }

        public void Dispose()
        {
            _history = null;
        }
    }
}
