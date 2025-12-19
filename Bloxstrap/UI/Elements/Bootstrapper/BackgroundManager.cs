using System;
using System.IO;
using System.Net.Http;
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
        private const int MaxHeight = 1080;
        private const string FallbackBackgroundUrl =
            "https://4kwallpapers.com/images/wallpapers/glacier-mountains-waterfall-watch-tower-moon-night-time-2560x1440-6404.png";

        private static readonly string CachePath =
            Path.Combine(Path.GetTempPath(), "voidstrap_bg_cache");

        public static async Task SetBackgroundAsync(Image imageControl, string? customPath)
        {
            if (imageControl == null) return;
            ApplyHighQualityScaling(imageControl);

            try
            {
                if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    await LoadFromPathAsync(imageControl, customPath);
                    return;
                }

                Console.WriteLine("[BackgroundManager] No local background. Using URL fallback.");
                string downloadedPath = await GetOrDownloadFallbackAsync();
                await LoadFromPathAsync(imageControl, downloadedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Background load failed: {ex.Message}");
                await ClearBackgroundAsync(imageControl);
            }
        }
        private static async Task LoadFromPathAsync(Image imageControl, string path)
        {
            await ClearBackgroundAsync(imageControl);

            string extension = Path.GetExtension(path);
            bool isGif = extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

            if (isGif)
                await LoadGifAsync(imageControl, path);
            else
                await LoadStaticImageAsync(imageControl, path);
        }
        private static async Task<string> GetOrDownloadFallbackAsync()
        {
            Directory.CreateDirectory(CachePath);

            string extension = Path.GetExtension(FallbackBackgroundUrl);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            string filePath = Path.Combine(CachePath, "fallback" + extension);

            if (File.Exists(filePath))
                return filePath;

            using var http = new HttpClient();
            byte[] data = await http.GetByteArrayAsync(FallbackBackgroundUrl);
            await File.WriteAllBytesAsync(filePath, data);

            return filePath;
        }
        private static async Task LoadGifAsync(Image imageControl, string path)
        {
            try
            {
                byte[] gifData = await Task.Run(() => File.ReadAllBytes(path));

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    _gifStream?.Dispose();
                    _gifStream = new MemoryStream(gifData, writable: false);

                    var gifBitmap = new BitmapImage();
                    gifBitmap.BeginInit();
                    gifBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    gifBitmap.StreamSource = _gifStream;
                    gifBitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.DelayCreation;
                    gifBitmap.DecodePixelWidth = MaxWidth;
                    gifBitmap.DecodePixelHeight = MaxHeight;
                    gifBitmap.EndInit();
                    gifBitmap.Freeze();

                    ImageBehavior.SetAnimatedSource(imageControl, gifBitmap);
                    ImageBehavior.SetRepeatBehavior(imageControl, System.Windows.Media.Animation.RepeatBehavior.Forever);

                    ApplyHighQualityScaling(imageControl);
                }, DispatcherPriority.Background);
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
                    bmp.CreateOptions = BitmapCreateOptions.DelayCreation;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                });

                await imageControl.Dispatcher.InvokeAsync(() =>
                {
                    ImageBehavior.SetAnimatedSource(imageControl, null);
                    imageControl.Source = bitmap;
                    ApplyHighQualityScaling(imageControl);
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundManager] Static image load failed: {ex.Message}");
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
            RenderOptions.SetBitmapScalingMode(imageControl, BitmapScalingMode.HighQuality);
            imageControl.SnapsToDevicePixels = true;
            imageControl.UseLayoutRounding = true;
        }
    }
}
