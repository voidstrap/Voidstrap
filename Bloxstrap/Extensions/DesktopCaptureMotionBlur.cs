using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace RobloxLightingOverlay
{
    public static class DesktopCapture
    {
        public static WriteableBitmap CaptureScreen(int width, int height)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            g.CopyFromScreen(0, 0, 0, 0, bmp.Size);

            var wb = new WriteableBitmap(
                width, height, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null);

            var data = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                bmp.PixelFormat);

            wb.Lock();
            unsafe
            {
                Buffer.MemoryCopy(
                    data.Scan0.ToPointer(),
                    wb.BackBuffer.ToPointer(),
                    wb.BackBufferStride * height,
                    wb.BackBufferStride * height);
            }
            wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
            wb.Unlock();

            bmp.UnlockBits(data);
            return wb;
        }
    }
}
