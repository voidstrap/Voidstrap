using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Voidstrap.Resources;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class ShortcutsViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly ConcurrentDictionary<string, (string Url, DateTime Expiry)> _gameIconCache = new();
        private static readonly ConcurrentDictionary<string, Task<string>> _ongoingRequests = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public ShortcutsViewModel()
        {
            _ = LoadGameIconAsync(GameID);
            LoadPrivateServerCode();
        }

        public bool IsStudioOptionVisible => App.IsStudioVisible;

        public ShortcutTask DesktopIconTask { get; } =
            new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");

        public ShortcutTask StartMenuIconTask { get; } =
            new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");

        public ShortcutTask PlayerIconTask { get; } =
            new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");

        public ShortcutTask StudioIconTask { get; } =
            new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");

        public ShortcutTask SettingsIconTask { get; } =
            new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");

        public ExtractIconsTask ExtractIconsTask { get; } = new();

        private bool _isPrivateServer;
        public bool IsPrivateServer
        {
            get => _isPrivateServer;
            set
            {
                if (_isPrivateServer != value)
                {
                    _isPrivateServer = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _privateServerCode;
        public string PrivateServerCode
        {
            get => _privateServerCode;
            set
            {
                if (_privateServerCode != value)
                {
                    if (TryParseShareLink(value, out string extractedCode))
                        _privateServerCode = extractedCode;
                    else
                        _privateServerCode = value;

                    OnPropertyChanged();
                }
            }
        }

        private string? _gameInstanceId;
        public string? GameInstanceId
        {
            get => _gameInstanceId;
            set
            {
                if (_gameInstanceId != value)
                {
                    _gameInstanceId = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _gameID = App.Settings.Prop.LaunchGameID;
        public string GameID
        {
            get => _gameID;
            set
            {
                if (_gameID != value)
                {
                    _gameID = value;
                    App.Settings.Prop.LaunchGameID = value;
                    OnPropertyChanged();
                    _ = LoadGameIconAsync(value);
                }
            }
        }

        private string _gameIconUrl;
        public string GameIconUrl
        {
            get => _gameIconUrl;
            set
            {
                _gameIconUrl = value;
                OnPropertyChanged();
                IsIconVisible = !string.IsNullOrEmpty(_gameIconUrl);
            }
        }

        private bool _isIconVisible;
        public bool IsIconVisible
        {
            get => _isIconVisible;
            set
            {
                _isIconVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isGameIconVisible;
        public bool IsGameIconVisible
        {
            get => _isGameIconVisible;
            set
            {
                if (_isGameIconVisible != value)
                {
                    _isGameIconVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _displayGameName;
        public string DisplayGameName
        {
            get => _displayGameName;
            set
            {
                _displayGameName = value;
                OnPropertyChanged();
            }
        }

        private string _gameName;
        public string GameName
        {
            get => _gameName;
            set
            {
                _gameName = value;
                OnPropertyChanged();
            }
        }
        private bool TryParseShareLink(string input, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(input)) return false;

            try
            {
                var uri = new Uri(input);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var typ = query["type"];
                var c = query["code"];
                if (!string.IsNullOrEmpty(c) &&
                    string.Equals(typ, "Server", StringComparison.OrdinalIgnoreCase))
                {
                    code = c;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void LoadPrivateServerCode()
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folderPath = Path.Combine(documentsPath, "Voidstrap");
                string privateCodePath = Path.Combine(folderPath, "PrivateServerCode.txt");

                if (File.Exists(privateCodePath))
                {
                    PrivateServerCode = File.ReadAllText(privateCodePath).Trim();
                }
            }
            catch { }
        }

        private async Task LoadGameIconAsync(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                GameIconUrl = null;
                GameName = null;
                IsGameIconVisible = false;
                return;
            }
            if (_gameIconCache.TryGetValue(gameId, out var cacheEntry))
            {
                if (cacheEntry.Expiry > DateTime.UtcNow)
                {
                    GameIconUrl = cacheEntry.Url;
                    IsGameIconVisible = !string.IsNullOrEmpty(cacheEntry.Url);
                    _ = LoadGameNameAsync(gameId);
                    return;
                }

                _gameIconCache.TryRemove(gameId, out _);
            }

            try
            {
                var fetchTask = _ongoingRequests.GetOrAdd(gameId, _ => FetchGameIconAsync(gameId));
                string imageUrl = await fetchTask.ConfigureAwait(false);

                _ongoingRequests.TryRemove(gameId, out _);
                _gameIconCache[gameId] = (imageUrl, DateTime.UtcNow.Add(CacheDuration));

                GameIconUrl = imageUrl;
                IsGameIconVisible = !string.IsNullOrEmpty(imageUrl);
                await LoadGameNameAsync(gameId);
            }
            catch
            {
                GameIconUrl = null;
                IsGameIconVisible = false;
                GameName = null;
            }
        }

        private async Task LoadGameNameAsync(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                GameName = DisplayGameName = "Unknown Game";
                return;
            }
            try
            {
                string uniUrl = $"https://apis.roblox.com/universes/v1/places/{gameId}/universe";
                string uniJson = await _httpClient.GetStringAsync(uniUrl).ConfigureAwait(false);
                using var uniDoc = JsonDocument.Parse(uniJson);

                if (!uniDoc.RootElement.TryGetProperty("universeId", out JsonElement uniElem))
                {
                    GameName = DisplayGameName = "Unknown Game";
                    return;
                }

                string universeId = uniElem.GetRawText().Trim('"');
                string detailsUrl = $"https://games.roblox.com/v1/games?universeIds={universeId}";
                string detailsJson = await _httpClient.GetStringAsync(detailsUrl).ConfigureAwait(false);
                using var detailsDoc = JsonDocument.Parse(detailsJson);

                var dataArray = detailsDoc.RootElement.GetProperty("data");
                if (dataArray.GetArrayLength() == 0)
                {
                    GameName = DisplayGameName = "Unknown Game";
                    return;
                }

                var gameInfo = dataArray[0];
                string name = gameInfo.GetProperty("name").GetString() ?? "Unknown Game";

                GameName = name;
                DisplayGameName = name;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadGameNameAsync failed: {ex.Message}");
                GameName = DisplayGameName = "Unknown Game";
            }
        }

        private async Task<string> FetchGameIconAsync(string gameId)
        {
            string url =
                $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={gameId}&returnPolicy=PlaceHolder&size=150x150&format=Png&isCircular=false";
            string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            using JsonDocument doc = JsonDocument.Parse(json);
            var dataElement = doc.RootElement.GetProperty("data");

            if (dataElement.GetArrayLength() > 0 &&
                dataElement[0].TryGetProperty("imageUrl", out JsonElement imageUrlElement))
            {
                return imageUrlElement.GetString();
            }

            return null;
        }
    }
}
