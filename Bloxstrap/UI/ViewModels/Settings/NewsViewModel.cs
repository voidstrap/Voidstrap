using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Voidstrap.UI.ViewModels.Settings
{
    internal sealed class NewsItemDto
    {
        public string? Title { get; set; }
        public string? Date { get; set; }
        public string? Content { get; set; }
        public string? ImageUrl { get; set; }
    }

    public sealed partial class NewsViewModel : ObservableObject, IDisposable
    {
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource _cts = new();

        private const string FeedUrl =
            "https://raw.githubusercontent.com/KloBraticc/VoidstrapNews-/main/news.json";

        private static string BasePath => Paths.Base;
        private string CachePath => Path.Combine(BasePath, "news_cache.json");
        private string ETagPath => Path.Combine(BasePath, "news_cache.etag");

        [ObservableProperty] private ObservableCollection<NewsItem> newsItems = new();
        [ObservableProperty] private string lastUpdatedText = "Loading...";
        [ObservableProperty] private bool isLoading = true;

        public NewsViewModel()
        {
            Directory.CreateDirectory(BasePath);

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Voidstrap", "1.0"));

            _ = SafeRefreshAsync();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await SafeRefreshAsync();
        }

        [RelayCommand]
        private void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenUrl ERROR] {ex}");
            }
        }

        private async Task SafeRefreshAsync()
        {
            if (!await _loadLock.WaitAsync(0))
                return;

            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            oldCts.Cancel();

            try
            {
                await LoadNewsAsync(_cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                oldCts.Dispose();
                _loadLock.Release();
            }
        }

        private async Task LoadNewsAsync(CancellationToken ct)
        {
            try
            {
                await SetLoadingAsync(true, "Loading...");

                string? json = null;
                bool fromCache = false;

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, FeedUrl);

                    if (File.Exists(ETagPath))
                    {
                        var etag = await SafeReadAllTextAsync(ETagPath, ct);
                        if (!string.IsNullOrWhiteSpace(etag))
                            req.Headers.TryAddWithoutValidation("If-None-Match", etag.Trim());
                    }

                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (resp.StatusCode == HttpStatusCode.NotModified && File.Exists(CachePath))
                    {
                        json = await SafeReadAllTextAsync(CachePath, ct);
                        fromCache = true;
                        Debug.WriteLine("[News] Using cached JSON (304 Not Modified).");
                    }
                    else
                    {
                        resp.EnsureSuccessStatusCode();
                        json = await resp.Content.ReadAsStringAsync(ct);
                        await SafeWriteAllTextAsync(CachePath, json, ct);

                        if (resp.Headers.ETag is not null)
                            await SafeWriteAllTextAsync(ETagPath, resp.Headers.ETag.ToString(), ct);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[News] Online fetch failed: {ex.Message}");
                    if (File.Exists(CachePath))
                    {
                        json = await SafeReadAllTextAsync(CachePath, ct);
                        fromCache = true;
                    }
                }

                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidOperationException("No news data available (network and cache unavailable).");

                var items = ParseNews(json);
                bool hasNewContent = !NewsItems.Select(n => n.Title).SequenceEqual(items.Select(i => i.Title));

                if (!hasNewContent && fromCache)
                {
                    await SetLoadingAsync(false, $"Last updated: {DateTime.Now:G} (no new items)");
                    return;
                }

                var imageTasks = items.Select(async item =>
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(item.ImageUrl))
                        return;

                    if (!Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out var uri))
                        return;

                    var fileName = SanitizeFileName(Path.GetFileName(uri.LocalPath) ?? $"img_{Guid.NewGuid():N}.bin");
                    var localPath = Path.Combine(BasePath, fileName);

                    try
                    {
                        if (!File.Exists(localPath) || !fromCache)
                        {
                            var data = await _http.GetByteArrayAsync(uri, ct);
                            await SafeWriteAllBytesAsync(localPath, data, ct);
                        }

                        var bmp = await LoadLocalImageAsync(localPath, ct);
                        if (bmp != null)
                            item.Image = bmp;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ImageLoad ERROR] {ex}");
                    }
                });

                await Task.WhenAll(imageTasks);

                await OnUiAsync(() =>
                {
                    NewsItems.Clear();
                    foreach (var n in items.OrderByDescending(i => i.Date))
                        NewsItems.Add(n);

                    LastUpdatedText = $"Last updated: {DateTime.Now:G}";
                    IsLoading = false;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadNewsAsync ERROR] {ex}");
                await OnUiAsync(() =>
                {
                    NewsItems.Clear();
                    NewsItems.Add(new NewsItem
                    {
                        Title = "Failed to load news",
                        Date = DateTime.Now,
                        Content = ex.Message
                    });
                    LastUpdatedText = $"Last checked: {DateTime.Now:G} (failed)";
                    IsLoading = false;
                });
            }
        }

        private static List<NewsItem> ParseNews(string json)
        {
            try
            {
                var token = JToken.Parse(json);
                JArray arr = token switch
                {
                    JArray a => a,
                    JObject o when o.TryGetValue("items", StringComparison.OrdinalIgnoreCase, out var t) && t is JArray ja => ja,
                    _ => throw new FormatException("Unexpected news format.")
                };

                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var dtos = arr.ToObject<NewsItemDto[]>(Newtonsoft.Json.JsonSerializer.Create(settings)) ?? Array.Empty<NewsItemDto>();

                static DateTime ParseDate(string? s) =>
                    DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d)
                        ? d.ToLocalTime()
                        : DateTime.MinValue;

                return dtos.Select(d => new NewsItem
                {
                    Title = d.Title?.Trim() ?? "Untitled",
                    Content = d.Content?.Trim() ?? string.Empty,
                    Date = ParseDate(d.Date),
                    ImageUrl = d.ImageUrl?.Trim() ?? string.Empty
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParseNews ERROR] {ex}");
                throw;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(fileName.Select(c => invalid.Contains(c) ? '_' : c));
        }

        private static async Task<string> SafeReadAllTextAsync(string path, CancellationToken ct)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            using var sr = new StreamReader(fs);
            ct.ThrowIfCancellationRequested();
            return await sr.ReadToEndAsync();
        }

        private static async Task SafeWriteAllTextAsync(string path, string content, CancellationToken ct)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            using var sw = new StreamWriter(fs);
            ct.ThrowIfCancellationRequested();
            await sw.WriteAsync(content.AsMemory(), ct);
        }

        private static async Task SafeWriteAllBytesAsync(string path, byte[] content, CancellationToken ct)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            ct.ThrowIfCancellationRequested();
            await fs.WriteAsync(content.AsMemory(0, content.Length), ct);
        }

        private static async Task<byte[]> SafeReadAllBytesAsync(string path, CancellationToken ct)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            var buffer = new byte[fs.Length];
            int bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                ct.ThrowIfCancellationRequested();
                bytesRead += await fs.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), ct);
            }
            return buffer;
        }

        private static async Task<BitmapImage?> LoadLocalImageAsync(string path, CancellationToken ct)
        {
            try
            {
                var data = await SafeReadAllBytesAsync(path, ct);
                return await Task.Run(() =>
                {
                    using var ms = new MemoryStream(data);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }, ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadLocalImageAsync ERROR] {ex}");
                return null;
            }
        }

        private static Task OnUiAsync(Action action)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
                return d.InvokeAsync(action).Task;

            action();
            return Task.CompletedTask;
        }

        private Task SetLoadingAsync(bool value, string? text = null) =>
            OnUiAsync(() =>
            {
                IsLoading = value;
                if (!string.IsNullOrWhiteSpace(text))
                    LastUpdatedText = text!;
            });

        public void Dispose()
        {
            try
            {
                _timer.Stop();
                _cts.Cancel();
                _http.Dispose();
                _cts.Dispose();
                _loadLock.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dispose ERROR] {ex}");
            }
        }
    }
}
