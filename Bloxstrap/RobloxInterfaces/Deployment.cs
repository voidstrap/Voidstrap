using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Voidstrap;

namespace Voidstrap.RobloxInterfaces
{
    public static class Deployment
    {
        public const string DefaultChannel = "production";
        private const string VersionStudioHash = "version-012732894899482c";
        public static string Channel { get; set; } = App.Settings.Prop.Channel;
        public static string BinaryType { get; set; } = "WindowsPlayer";
        public static bool IsDefaultChannel => string.Equals(Channel, DefaultChannel, StringComparison.OrdinalIgnoreCase);
        public static string BaseUrl { get; private set; } = string.Empty;

        public static readonly HashSet<HttpStatusCode?> BadChannelCodes = new()
        {
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound
        };

        private static readonly ConcurrentDictionary<string, ClientVersion> ClientVersionCache = new();

        private static readonly Dictionary<string, int> BaseUrls = new()
        {
            { "https://setup.rbxcdn.com", 0 },
            { "https://setup-aws.rbxcdn.com", 2 },
            { "https://setup-ak.rbxcdn.com", 2 },
            { "https://roblox-setup.cachefly.net", 2 },
            { "https://s3.amazonaws.com/setup.roblox.com", 4 }
        };

        public static string GetInfoUrl(string channel)
        {
            bool isDefault = string.Equals(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase);
            string path = isDefault
                ? $"/v2/client-version/{BinaryType}"
                : $"/v2/client-version/{BinaryType}/channel/{channel}";
            return $"https://clientsettingscdn.roblox.com{path}";
        }

        private static async Task<T> SafeGetJson<T>(string url)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VoidstrapUpdater/1.0");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("<"))
            {
                throw new InvalidHTTPResponseException(
                    $"Expected JSON but got HTML or empty response from {url}:\n{text.Substring(0, Math.Min(300, text.Length))}");
            }

            try
            {
                return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidHTTPResponseException($"Failed to deserialize JSON from {url}");
            }
            catch (JsonException ex)
            {
                throw new InvalidHTTPResponseException($"Invalid JSON from {url}: {ex.Message}. Snippet: {text.Substring(0, Math.Min(300, text.Length))}");
            }
        }
        private static async Task<string?> TestConnection(string url, int delaySeconds, CancellationToken token)
        {
            string logIdent = $"Deployment::TestConnection<{url}>";

            try
            {
                await Task.Delay(delaySeconds * 1000, token);
                App.Logger.WriteLine(logIdent, "Connecting...");

                using var response = await App.HttpClient.GetAsync($"{url}/versionStudio", token);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(token);
                if (!string.Equals(content.Trim(), VersionStudioHash, StringComparison.Ordinal))
                    throw new InvalidHTTPResponseException($"Expected {VersionStudioHash}, got {content}");

                return url;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                App.Logger.WriteException(logIdent, ex);
                return null;
            }
        }

        public static async Task<Exception?> InitializeConnectivity()
        {
            const string logIdent = "Deployment::InitializeConnectivity";
            using var cts = new CancellationTokenSource();
            var tasks = BaseUrls.Select(entry => TestConnection(entry.Key, entry.Value, cts.Token)).ToList();

            App.Logger.WriteLine(logIdent, "Testing connectivity...");

            while (tasks.Any() && string.IsNullOrEmpty(BaseUrl))
            {
                var finished = await Task.WhenAny(tasks);
                tasks.Remove(finished);

                var result = await finished;
                if (!string.IsNullOrEmpty(result))
                {
                    BaseUrl = result;
                    cts.Cancel();
                }
            }

            if (string.IsNullOrEmpty(BaseUrl))
                return new Exception("Failed to connect to any setup mirrors.");

            App.Logger.WriteLine(logIdent, $"Using base URL: {BaseUrl}");
            return null;
        }

        public static string GetLocation(string resource)
        {
            var location = BaseUrl;
            if (!IsDefaultChannel)
            {
                var useCommon = ApplicationSettings
                    .GetSettings(nameof(ApplicationSettings.PCClientBootstrapper), Channel)
                    .Get<bool>("FFlagReplaceChannelNameForDownload");

                var channelName = useCommon ? "common" : Channel.ToLowerInvariant();
                location += $"/channel/{channelName}";
            }

            return $"{location}{resource}";
        }

        public static async Task<ClientVersion> GetInfo(string? inputChannel = null)
        {
            const string logIdent = "Deployment::GetInfo";
            var channel = string.IsNullOrEmpty(inputChannel) ? Channel : inputChannel;
            bool isDefault = string.Equals(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase);

            App.Logger.WriteLine(logIdent, $"Fetching deploy info for channel {channel}");

            string cacheKey = $"{channel}-{BinaryType}";
            if (ClientVersionCache.TryGetValue(cacheKey, out var cachedVersion))
            {
                App.Logger.WriteLine(logIdent, "Using cached deploy info");
                return cachedVersion;
            }

            string cdnUrl = GetInfoUrl(channel);
            string fallbackUrl = cdnUrl.Replace("clientsettingscdn.roblox.com", "clientsettings.roblox.com");

            ClientVersion clientVersion;

            try
            {
                clientVersion = await SafeGetJson<ClientVersion>(cdnUrl);
            }
            catch (HttpRequestException ex) when (!isDefault && BadChannelCodes.Contains(ex.StatusCode))
            {
                throw new InvalidChannelException(ex.StatusCode);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"CDN failed for {channel}, falling back. Reason: {ex.Message}");
                App.Logger.WriteException(logIdent, ex);

                clientVersion = await SafeGetJson<ClientVersion>(fallbackUrl);
            }
            if (!isDefault)
            {
                var defaultVer = await GetInfo(DefaultChannel);
                if (Utilities.CompareVersions(clientVersion.Version, defaultVer.Version) == VersionComparison.LessThan)
                    clientVersion.IsBehindDefaultChannel = true;
            }

            ClientVersionCache.TryAdd(cacheKey, clientVersion);
            return clientVersion;
        }

        public static async Task<(string luaPackagesZip, string extraTexturesZip, string contentTexturesZip, string versionHash, string version)>
            DownloadForModGenerator(bool overwrite = false)
        {
            const string LOG_IDENT = "Deployment::DownloadForModGenerator";
            try
            {
                var clientInfo = await SafeGetJson<ClientVersion>("https://clientsettingscdn.roblox.com/v2/client-version/WindowsStudio64");
                if (string.IsNullOrEmpty(clientInfo.VersionGuid) || !clientInfo.VersionGuid.StartsWith("version-"))
                    throw new InvalidHTTPResponseException("Invalid clientVersionUpload from Roblox API.");

                string versionHash = clientInfo.VersionGuid["version-".Length..];
                string version = clientInfo.Version;

                string tmp = Path.Combine(Path.GetTempPath(), "Voidstrap");
                Directory.CreateDirectory(tmp);

                string luaPackagesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-luapackages.zip";
                string extraTexturesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-textures.zip";
                string contentTexturesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-content-textures2.zip";

                string luaPackagesZip = Path.Combine(tmp, $"extracontent-luapackages-{versionHash}.zip");
                string extraTexturesZip = Path.Combine(tmp, $"extracontent-textures-{versionHash}.zip");
                string contentTexturesZip = Path.Combine(tmp, $"content-textures2-{versionHash}.zip");

                async Task<string> DownloadFile(string url, string path)
                {
                    if (File.Exists(path))
                    {
                        if (!overwrite)
                        {
                            var fi = new FileInfo(path);
                            if (fi.Length > 0)
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Reusing existing file: {path}");
                                return path;
                            }
                            File.Delete(path);
                        }
                        else File.Delete(path);
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Downloading {url} -> {path}");
                    using var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
                    using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);

                    return path;
                }

                luaPackagesZip = await DownloadFile(luaPackagesUrl, luaPackagesZip);
                extraTexturesZip = await DownloadFile(extraTexturesUrl, extraTexturesZip);
                contentTexturesZip = await DownloadFile(contentTexturesUrl, contentTexturesZip);

                App.Logger.WriteLine(LOG_IDENT, $"All downloads complete for version {versionHash}.");
                return (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }
        }
    }
}
