using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using Wpf.Ui.Controls;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class HubPage : UiPage
    {
        private static readonly Uri ReleasesApiUri =
            new("https://api.github.com/repos/voidstrap/Voidstrap/releases");

        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly string CacheFile =
            Path.Combine(Paths.Base, "Releases.json");

        public ObservableCollection<GithubRelease> Releases { get; } = new();
        private readonly ICollectionView _releasesView;

        private FileSystemWatcher? _cacheWatcher;
        private string? _etag;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "VoidstrapApp/1.0 (+https://github.com/voidstrap/Voidstrap)");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public HubPage()
        {
            InitializeComponent();
            DataContext = this;

            _releasesView = CollectionViewSource.GetDefaultView(Releases);

            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);

            StartCacheWatcher();
            StartAutoRefresh();

            _ = LoadReleasesAsync(force: true);
        }

        private void StartAutoRefresh()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(_refreshInterval);
                    await LoadReleasesAsync();
                }
            });
        }

        private void StartCacheWatcher()
        {
            _cacheWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(CacheFile)!,
                Filter = Path.GetFileName(CacheFile),
                NotifyFilter = NotifyFilters.LastWrite |
                               NotifyFilters.Size |
                               NotifyFilters.FileName
            };

            _cacheWatcher.Changed += (_, __) => Dispatcher.Invoke(async () =>
                await LoadFromCacheAsync());

            _cacheWatcher.Created += (_, __) => Dispatcher.Invoke(async () =>
                await LoadFromCacheAsync());

            _cacheWatcher.EnableRaisingEvents = true;
        }

        private async Task LoadReleasesAsync(bool force = false)
        {
            await LoadFromCacheAsync();

            try
            {
                using var request =
                    new HttpRequestMessage(HttpMethod.Get, ReleasesApiUri);

                if (!force && !string.IsNullOrEmpty(_etag))
                    request.Headers.IfNoneMatch.Add(
                        new EntityTagHeaderValue(_etag));

                using var response = await HttpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotModified)
                    return;

                response.EnsureSuccessStatusCode();

                _etag = response.Headers.ETag?.Tag;

                var json = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(CacheFile, json);

                var releases = JsonSerializer.Deserialize<GithubRelease[]>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? Array.Empty<GithubRelease>();

                UpdateReleasesCollection(releases);
            }
            catch
            {
            }
        }

        private async Task LoadFromCacheAsync()
        {
            if (!File.Exists(CacheFile))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(CacheFile);

                var releases = JsonSerializer.Deserialize<GithubRelease[]>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? Array.Empty<GithubRelease>();

                UpdateReleasesCollection(releases);
            }
            catch
            {
            }
        }

        private void UpdateReleasesCollection(GithubRelease[] releases)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Releases.Clear();
                foreach (var rel in releases)
                {
                    rel.CalculateTotals();
                    Releases.Add(rel);
                }
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query =
                (sender as System.Windows.Forms.TextBox)?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                _releasesView.Filter = null;
            }
            else
            {
                _releasesView.Filter = obj =>
                {
                    if (obj is not GithubRelease r) return false;

                    bool Matches(string? s) =>
                        !string.IsNullOrEmpty(s) &&
                        s.IndexOf(query,
                            StringComparison.OrdinalIgnoreCase) >= 0;

                    return Matches(r.Name) ||
                           Matches(r.TagName) ||
                           Matches(r.Body);
                };
            }

            _releasesView.Refresh();
        }

        private void Hyperlink_RequestNavigate(
            object sender,
            RequestNavigateEventArgs e)
        {
            try
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public class GithubRelease
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("published_at")]
            public DateTimeOffset PublishedAt { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("assets")]
            public GithubAsset[] Assets { get; set; } = Array.Empty<GithubAsset>();

            public int TotalDownloads { get; private set; }

            public void CalculateTotals()
            {
                TotalDownloads =
                    Assets?.Sum(a => a.DownloadCount) ?? 0;
            }
        }

        public class GithubAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("content_type")]
            public string? ContentType { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("download_count")]
            public int DownloadCount { get; set; }

            public double SizeMb =>
                Size / 1024d / 1024d;
        }
    }
}
