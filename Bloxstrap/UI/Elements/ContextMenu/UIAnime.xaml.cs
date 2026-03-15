using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Common;
using static System.Net.Mime.MediaTypeNames;

namespace Voidstrap.UI.Elements.Overlay
{
    public partial class AnimeWindow : Window
    {
        private bool _isFullscreen = false;
        private readonly string saveFilePath;
        private bool _fullscreenNotificationShown = false;
        private double _prevLeft, _prevTop, _prevWidth, _prevHeight;
        private readonly string allowedUrl = "https://aniwatchtv.to/home";
        private const string DISCORD_CLIENT_ID = "1481518581354336336";

        public AnimeWindow()
        {
            InitializeComponent();
            MainBorder.MouseLeftButtonDown += DragWindow;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Voidstrap");
            Directory.CreateDirectory(folder);
            saveFilePath = Path.Combine(folder, "OPOS.txt");
            LoadWindowPosition();
            DiscordManager.Initialize(DISCORD_CLIENT_ID);
            InitializeWebView();
            Loaded += AnimeWindow_Loaded;
            this.LocationChanged += AnimeWindow_LocationChanged;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !_isFullscreen)
            {
                try
                {
                    this.DragMove();
                }
                catch {}
            }
        }

        private void AnimeWindow_LocationChanged(object sender, EventArgs e)
        {
            if (!_isFullscreen)
            {
                SaveWindowPosition();
            }
        }

        private void SaveWindowPosition()
        {
            try
            {
                string data = $"{this.Left},{this.Top}";
                File.WriteAllText(saveFilePath, data);
            }
            catch {}
        }

        private void LoadWindowPosition()
        {
            if (File.Exists(saveFilePath))
            {
                string[] parts = File.ReadAllText(saveFilePath).Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double savedLeft) &&
                    double.TryParse(parts[1], out double savedTop))
                {
                    var screenBounds = Screen.PrimaryScreen.WorkingArea;
                    if (savedLeft >= 0 && savedLeft + this.Width <= screenBounds.Width &&
                        savedTop >= 0 && savedTop + this.Height <= screenBounds.Height)
                    {
                        Left = savedLeft;
                        Top = savedTop;
                        return;
                    }
                }
            }

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private async void InitializeWebView()
        {
            await AnimeWebView.EnsureCoreWebView2Async();
            var core = AnimeWebView.CoreWebView2;

            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;

            #if DEBUG
            core.Settings.AreDevToolsEnabled = true;
            #else
            core.Settings.AreDevToolsEnabled = false;
#endif

            string css = @"
    #ads, .ad-container, .popup, .modal, .banner, .overlay,
    .promo, .promotion, .deal-banner, .video-ad, .advertisement {
        display: none !important;
    }
    iframe { width:100% !important; height:100% !important; border:none !important; }
    * { scroll-behavior: smooth !important; }
";

            string injectCssScript = $@"
        let style = document.createElement('style');
        style.innerHTML = `{css}`;
        document.head.appendChild(style);
    ";
            await core.AddScriptToExecuteOnDocumentCreatedAsync(injectCssScript);
            string observeEpScript = @"
        (function() {
            function sendEpisode() {
                if (window.location.pathname.startsWith('/home')) return;

                const activeEp = document.querySelector('.ss-list .ssl-item.ep-item.active');
                const epNumber = activeEp ? activeEp.getAttribute('data-number') : null;

                const posterImg = document.querySelector('.film-poster img');
                const posterSrc = posterImg ? posterImg.src : null;

                window.chrome.webview.postMessage(JSON.stringify({ number: epNumber, poster: posterSrc }));
            }

            const observer = new MutationObserver(sendEpisode);
            observer.observe(document.body, { childList: true, subtree: true, attributes: true });
            setTimeout(sendEpisode, 800);
        })();
    ";
            await core.AddScriptToExecuteOnDocumentCreatedAsync(observeEpScript);
            // block any (IF SO)
            string[] blockedKeywords = new string[]
            {
    "ads",
    "doubleclick",
    "googlesyndication",
    "promo",
    "banner",
    "deal",
    "video-ad",
    "advertisement",
    "noozy.tv",
    "https://piccdn.net/blank-728x90-aniw.gif"
            };

            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Script);
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Image);
            core.WebResourceRequested += (sender, args) =>
            {
                string uri = args.Request.Uri.ToLower();
                foreach (var word in blockedKeywords)
                {
                    if (uri.Contains(word))
                    {
                        args.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
                        return;
                    }
                }
            };

            // Controls of nav very nice
            core.NavigationStarting += (sender, args) =>
            {
                if (!args.Uri.StartsWith("https://aniwatchtv.to", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            core.NewWindowRequested += (sender, args) =>
            {
                if (args.Uri.Contains("login") || args.Uri.Contains("signup"))
                {
                    AnimeWebView.Source = new Uri(args.Uri);
                }
                args.Handled = true;
            };

            core.WebMessageReceived += (sender, args) =>
            {
                try
                {
                    string url = AnimeWebView.Source.ToString();

                    if (url.Contains("/home"))
                    {
                        DiscordManager.UpdatePresence(
                            "Browsing Anime",
                            url,
                            new (string, string)[] { ("AniWatch Home", "https://aniwatchtv.to/home") },
                            null
                        );
                        return;
                    }

                    string json = args.WebMessageAsJson.Trim('"').Replace("\\\"", "\"");
                    var epInfo = System.Text.Json.JsonSerializer.Deserialize<EpisodeInfo>(json);

                    string epNumber = epInfo?.number;
                    string posterUrl = epInfo?.poster;

                    string animeTitle = GetAnimeTitleFromUrl(url);
                    string discordLabel = epNumber != null ? $"EP | {epNumber} → {animeTitle}" : animeTitle;
                    string animeDetailsUrl = GetAnimeDetailsUrl(url);

                    DiscordManager.UpdatePresence(
                        discordLabel,
                        url,
                        new (string label, string link)[] { (discordLabel, url), ("Anime Details", animeDetailsUrl) },
                        posterUrl
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to update episode info dynamically: " + ex.Message);
                }
            };

            core.NavigationCompleted += async (sender, args) =>
            {
                if (!args.IsSuccess)
                    return;

                try
                {
                    await Task.Delay(500);
                    string script = @"
        (function() {
            const activeEp = document.querySelector('.ss-list .ssl-item.ep-item.active');
            const epNumber = activeEp ? activeEp.getAttribute('data-number') : null;
            const posterImg = document.querySelector('.film-poster img');
            const posterSrc = posterImg ? posterImg.src : null;
            return JSON.stringify({ number: epNumber, poster: posterSrc });
        })();
        ";

                    string rawResult = await core.ExecuteScriptAsync(script);
                    string url = AnimeWebView.Source.ToString();
                    if (url.Contains("/home"))
                    {
                        DiscordManager.UpdatePresence(
                            "Browsing Anime",
                            url,
                            new (string, string)[] { ("AniWatch Home", "https://aniwatchtv.to/home") },
                            null
                        );
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(rawResult) && rawResult != "null")
                    {
                        rawResult = rawResult.Trim('"').Replace("\\\"", "\"");
                        var epInfo = System.Text.Json.JsonSerializer.Deserialize<EpisodeInfo>(rawResult);

                        string epNumber = epInfo?.number;
                        string posterUrl = epInfo?.poster;
                        string animeTitle = GetAnimeTitleFromUrl(url);
                        string discordLabel = epNumber != null ? $"EP | {epNumber} → {animeTitle}" : animeTitle;
                        string animeDetailsUrl = GetAnimeDetailsUrl(url);

                        DiscordManager.UpdatePresence(
                            discordLabel,
                            url,
                            new (string label, string link)[] { (discordLabel, url), ("Anime Details", animeDetailsUrl) },
                            posterUrl
                        );
                    }
                }
                catch
                {
                    string urlFallback = AnimeWebView.Source.ToString();
                    DiscordManager.UpdatePresence(urlFallback, urlFallback, new (string, string)[0], null);
                }
            };

            AnimeWebView.Source = new Uri(allowedUrl);
        }

        private string GetAnimeTitleFromUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && segments[0] == "watch")
                {
                    string slug = System.Text.RegularExpressions.Regex.Replace(segments[1], @"-\d+$", "");
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                        .ToTitleCase(slug.Replace("-", " "));
                }
            }
            catch { }
            return "AniWatch - Home";
        }

        private string GetAnimeDetailsUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && segments[0] == "watch")
                {
                    return $"https://aniwatchtv.to/{segments[1]}";
                }
            }
            catch { }
            return url;
        }

        private void AnimeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Show();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void FadeIn()
        {
            MainBorder.Opacity = 0;
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            MainBorder.BeginAnimation(OpacityProperty, fade);
        }

        public void FadeOut()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            fade.Completed += (_, __) => Hide();
            MainBorder.BeginAnimation(OpacityProperty, fade);
        }

        private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Width - e.HorizontalChange;
            double newHeight = Height - e.VerticalChange;
            if (newWidth >= MinWidth) { Width = newWidth; Left += e.HorizontalChange; }
            if (newHeight >= MinHeight) { Height = newHeight; Top += e.VerticalChange; }
        }

        private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height - e.VerticalChange;
            if (newWidth >= MinWidth) Width = newWidth;
            if (newHeight >= MinHeight) { Height = newHeight; Top += e.VerticalChange; }
        }

        private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Width - e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;
            if (newWidth >= MinWidth) { Width = newWidth; Left += e.HorizontalChange; }
            if (newHeight >= MinHeight) Height = newHeight;
        }

        private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;
            if (newWidth >= MinWidth) Width = newWidth;
            if (newHeight >= MinHeight) Height = Height;
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _isFullscreen)
                FullscreenButton_Click(null, null);

            base.OnKeyDown(e);
        }

        private async void Watch2GetherButton_Click(object sender, RoutedEventArgs e)
        {
            string script = @"
        const btn = document.querySelector('a[href^=""/watch2gether""]');
        if(btn) btn.click();
    ";
            await AnimeWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async void RandomButton_Click(object sender, RoutedEventArgs e)
        {
            string script = @"
        const btn = document.querySelector('a[href^=""/random""]');
        if(btn) btn.click();
    ";
            await AnimeWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async void CommunityButton_Click(object sender, RoutedEventArgs e)
        {
            string script = @"
        const btn = document.querySelector('a[href^=""/community/board""]');
        if(btn) btn.click();
    ";
            await AnimeWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        public class EpisodeInfo
        {
            public string number { get; set; }
            public string poster { get; set; }
        }

        private async void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFullscreen)
            {
                _prevLeft = Left;
                _prevTop = Top;
                _prevWidth = Width;
                _prevHeight = Height;

                MainGrid.RowDefinitions[1].Height = new GridLength(0);
                MainBorder.Padding = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(0);

                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;

                _isFullscreen = true;

                if (!_fullscreenNotificationShown)
                {
                    _fullscreenNotificationShown = true;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (!(App.Current.Resources["NotificationWindow"] is NotificationWindow notificationWindow))
                            {
                                notificationWindow = new NotificationWindow();
                                App.Current.Resources["NotificationWindow"] = notificationWindow;
                            }

                            if (!notificationWindow.IsVisible)
                                notificationWindow.Show();

                            notificationWindow.Activate();
                            notificationWindow.ShowNotification("Press 'ESC' To Exit Fullscreen!", "https://online.fliphtml5.com/rxkgl/rnfj/files/large/95e7a87a9e858ca0085f76054ed3a16d.webp?1701104491", 4
                            );
                        }
                        catch { }
                    });
                }
            }
            else
            {
                MainGrid.RowDefinitions[1].Height = new GridLength(40);
                MainBorder.Padding = new Thickness(4);
                MainBorder.CornerRadius = new CornerRadius(4);

                Left = _prevLeft;
                Top = _prevTop;
                Width = _prevWidth;
                Height = _prevHeight;

                _isFullscreen = false;
                _fullscreenNotificationShown = false;
            }
        }
    }
}