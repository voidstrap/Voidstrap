using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public static class BackgroundManager
    {
        private static MemoryStream? _gifStream;
        private const int MaxWidth = 1920;
        private const int MaxHeight = 1080;

        public static async Task SetBackgroundAsync(Image imageControl, string? customPath)
        {
            if (imageControl == null)
                return;

            if (string.IsNullOrWhiteSpace(customPath) || !File.Exists(customPath))
            {
                Console.WriteLine("[BackgroundManager] No valid custom background provided.");
                await ClearBackgroundAsync(imageControl);
                return;
            }

            bool isGif = Path.GetExtension(customPath)
                .Equals(".gif", StringComparison.OrdinalIgnoreCase);

            try
            {
                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    ImageBehavior.SetAnimatedSource(imageControl, null);
                    imageControl.Source = null;
                }, DispatcherPriority.Render);

                if (isGif)
                {
                    await LoadGifAsync(imageControl, customPath);
                }
                else
                {
                    await LoadStaticImageAsync(imageControl, customPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Failed to load background: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static async Task LoadGifAsync(Image imageControl, string path)
        {
            try
            {
                byte[] gifData = await File.ReadAllBytesAsync(path).ConfigureAwait(false);

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    _gifStream?.Dispose();
                    _gifStream = new MemoryStream(gifData, writable: false);

                    var gifBitmap = new BitmapImage();
                    gifBitmap.BeginInit();
                    gifBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    gifBitmap.StreamSource = _gifStream;
                    gifBitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    gifBitmap.DecodePixelWidth = MaxWidth;
                    gifBitmap.DecodePixelHeight = MaxHeight;
                    gifBitmap.EndInit();
                    gifBitmap.Freeze();

                    ImageBehavior.SetAnimatedSource(imageControl, gifBitmap);
                    ImageBehavior.SetRepeatBehavior(
                        imageControl,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] GIF load failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static async Task LoadStaticImageAsync(Image imageControl, string path)
        {
            try
            {
                var bitmap = await Task.Run(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.DecodePixelWidth = MaxWidth;
                    bmp.DecodePixelHeight = MaxHeight;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }).ConfigureAwait(false);

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    ImageBehavior.SetAnimatedSource(imageControl, null);
                    imageControl.Source = bitmap;
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Static image load failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static async Task ClearBackgroundAsync(Image imageControl)
        {
            await imageControl.Dispatcher.InvokeAsync(() =>
            {
                ImageBehavior.SetAnimatedSource(imageControl, null);
                imageControl.Source = null;
            }, DispatcherPriority.Render);
        }
    }
}
