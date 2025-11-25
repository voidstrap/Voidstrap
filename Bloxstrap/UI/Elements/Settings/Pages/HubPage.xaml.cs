using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using Wpf.Ui.Controls;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class HubPage : UiPage
    {
        private static readonly Uri ReleasesApiUri =
            new("https://api.github.com/repos/voidstrap/Voidstrap/releases");
        public ObservableCollection<GithubRelease> Releases { get; } = new();

        private readonly ICollectionView _releasesView;

        public HubPage()
        {
            InitializeComponent();

            DataContext = this;

            _releasesView = CollectionViewSource.GetDefaultView(Releases);

            _ = LoadReleasesAsync();
        }

        private async Task LoadReleasesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "VoidstrapApp/1.0 (+https://github.com/voidstrap/Voidstrap)");

                var json = await client.GetStringAsync(ReleasesApiUri);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var releases = JsonSerializer.Deserialize<GithubRelease[]>(json, options)
                               ?? Array.Empty<GithubRelease>();

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
            catch (Exception)
            {
            }
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
                query = query.ToLowerInvariant();

                _releasesView.Filter = obj =>
                {
                    if (obj is not GithubRelease r)
                        return false;

                    bool Matches(string? s) =>
                        !string.IsNullOrEmpty(s) &&
                        s.ToLowerInvariant().Contains(query);

                    return Matches(r.Name)
                           || Matches(r.TagName)
                           || Matches(r.Body);
                };
            }

            _releasesView.Refresh();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                e.Handled = true;

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
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
                TotalDownloads = Assets?.Sum(a => a.DownloadCount) ?? 0;
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

            public double SizeMb => Size / 1024d / 1024d;
        }
    }
}
