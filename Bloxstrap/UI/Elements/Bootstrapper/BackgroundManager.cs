using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public static class BackgroundManager
    {
        private static MemoryStream? _gifStream;

        private const int MaxWidth = 1920;

        public static async Task SetBackgroundAsync(Image imageControl, string? customPath)
        {
            if (imageControl == null)
                return;

            ApplyHighQualityScaling(imageControl);

            try
            {
                if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    await LoadFromPathAsync(imageControl, customPath);
                }
                else
                {
                    await ClearBackgroundAsync(imageControl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static async Task LoadFromPathAsync(Image imageControl, string path)
        {
            await ClearBackgroundAsync(imageControl);

            bool isGif = Path.GetExtension(path)
                .Equals(".gif", StringComparison.OrdinalIgnoreCase);

            if (isGif)
                await LoadGifAsync(imageControl, path);
            else
                await LoadStaticImageAsync(imageControl, path);
        }

        private static async Task LoadGifAsync(Image imageControl, string path)
        {
            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    _gifStream?.Dispose();
                    _gifStream = new MemoryStream(data, writable: false);

                    var gif = new BitmapImage();
                    gif.BeginInit();
                    gif.CacheOption = BitmapCacheOption.OnLoad;
                    gif.StreamSource = _gifStream;
                    gif.DecodePixelWidth = MaxWidth;
                    gif.EndInit();
                    gif.Freeze();

                    ImageBehavior.SetAnimatedSource(imageControl, gif);
                    ImageBehavior.SetRepeatBehavior(
                        imageControl,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);

                    ApplyHighQualityScaling(imageControl);
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] GIF failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static async Task LoadStaticImageAsync(Image imageControl, string path)
        {
            try
            {
                BitmapImage bitmap = await Task.Run(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.DecodePixelWidth = MaxWidth;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                });

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    ImageBehavior.SetAnimatedSource(imageControl, null);
                    imageControl.Source = bitmap;
                    ApplyHighQualityScaling(imageControl);
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Image failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }

        private static Task ClearBackgroundAsync(Image imageControl)
        {
            return imageControl.Dispatcher.InvokeAsync(() =>
            {
                ImageBehavior.SetAnimatedSource(imageControl, null);
                imageControl.Source = null;
            }, DispatcherPriority.Render).Task;
        }

        private static void ApplyHighQualityScaling(Image imageControl)
        {
            RenderOptions.SetBitmapScalingMode(
                imageControl,
                BitmapScalingMode.HighQuality);

            imageControl.SnapsToDevicePixels = true;
            imageControl.UseLayoutRounding = true;
        }
    }
}