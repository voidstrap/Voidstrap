using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private static readonly string CacheFile = Path.Combine(Paths.Base, "Releases.json");

        public ObservableCollection<GithubRelease> Releases { get; } = new();

        private readonly ICollectionView _releasesView;

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VoidstrapApp/1.0 (+https://github.com/voidstrap/Voidstrap)");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public HubPage()
        {
            InitializeComponent();
            DataContext = this;
            _releasesView = CollectionViewSource.GetDefaultView(Releases);
            _ = LoadReleasesAsync();
        }

        private async Task LoadReleasesAsync()
        {
            GithubRelease[] releases = Array.Empty<GithubRelease>();
            if (File.Exists(CacheFile))
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(CacheFile);
                    releases = JsonSerializer.Deserialize<GithubRelease[]>(cachedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<GithubRelease>();
                }
                catch
                {
                    releases = Array.Empty<GithubRelease>();
                }
            }

            UpdateReleasesCollection(releases);
            try
            {
                var json = await HttpClient.GetStringAsync(ReleasesApiUri).ConfigureAwait(true);
                var latestReleases = JsonSerializer.Deserialize<GithubRelease[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<GithubRelease>();
                if (!releases.SequenceEqual(latestReleases, new GithubReleaseComparer()))
                {
                    await File.WriteAllTextAsync(CacheFile, json);
                    UpdateReleasesCollection(latestReleases);
                }
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
            var query = (sender as System.Windows.Controls.TextBox)?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                _releasesView.Filter = null;
            }
            else
            {
                _releasesView.Filter = obj =>
                {
                    if (obj is not GithubRelease r) return false;
                    bool Matches(string? s) => !string.IsNullOrEmpty(s) && s.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    return Matches(r.Name) || Matches(r.TagName) || Matches(r.Body);
                };
            }

            _releasesView.Refresh();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                e.Handled = true;
                var psi = new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true };
                Process.Start(psi);
            }
            catch { }
        }

        public class GithubRelease
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("tag_name")] public string? TagName { get; set; }
            [JsonPropertyName("body")] public string? Body { get; set; }
            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
            [JsonPropertyName("draft")] public bool Draft { get; set; }
            [JsonPropertyName("published_at")] public DateTimeOffset PublishedAt { get; set; }
            [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
            [JsonPropertyName("assets")] public GithubAsset[] Assets { get; set; } = Array.Empty<GithubAsset>();

            public int TotalDownloads { get; private set; }
            public void CalculateTotals() => TotalDownloads = Assets?.Sum(a => a.DownloadCount) ?? 0;
        }

        public class GithubAsset
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("content_type")] public string? ContentType { get; set; }
            [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
            [JsonPropertyName("size")] public long Size { get; set; }
            [JsonPropertyName("download_count")] public int DownloadCount { get; set; }
            public double SizeMb => Size / 1024d / 1024d;
        }

        private class GithubReleaseComparer : IEqualityComparer<GithubRelease>
        {
            public bool Equals(GithubRelease? x, GithubRelease? y)
            {
                if (x == null || y == null) return false;
                return x.TagName == y.TagName;
            }

            public int GetHashCode(GithubRelease obj) => obj.TagName?.GetHashCode() ?? 0;
        }
    }
}
