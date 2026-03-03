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
        public static bool IsDefaultChannel =>
            string.Equals(Channel, DefaultChannel, StringComparison.OrdinalIgnoreCase);

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

        private static readonly HttpClient SharedHttp = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static Deployment()
        {
            SharedHttp.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VoidstrapUpdater/2.0");
            SharedHttp.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

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
            using var response = await SharedHttp.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Request failed: {(int)response.StatusCode}",
                    null,
                    response.StatusCode);

            string text = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("<"))
                throw new InvalidHTTPResponseException($"Invalid JSON response from {url}");

            return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidHTTPResponseException($"Failed to deserialize JSON from {url}");
        }

        public static async Task<ClientVersion> GetInfo(
            string? inputChannel = null,
            IEnumerable<string>? cycleChannels = null)
        {
            const string logIdent = "Deployment::GetInfo";

            var channel = string.IsNullOrEmpty(inputChannel) ? Channel : inputChannel;
            bool isDefault = string.Equals(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase);

            App.Logger.WriteLine(logIdent, $"Fetching deploy info for channel {channel}");

            string cacheKey = $"{channel}-{BinaryType}";
            if (ClientVersionCache.TryGetValue(cacheKey, out var cached))
            {
                App.Logger.WriteLine(logIdent, "Using cached deploy info");
                return cached;
            }

            try
            {
                var version = await SafeGetJson<ClientVersion>(GetInfoUrl(channel));
                ClientVersionCache.TryAdd(cacheKey, version);
                return version;
            }
            catch (HttpRequestException ex) when (!isDefault && BadChannelCodes.Contains(ex.StatusCode))
            {
                App.Logger.WriteLine(logIdent,
                    $"Channel {channel} failed ({ex.StatusCode}).");

                if (cycleChannels != null)
                {
                    foreach (var next in cycleChannels.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            App.Logger.WriteLine(logIdent, $"Trying next channel: {next}");

                            var info = await SafeGetJson<ClientVersion>(GetInfoUrl(next));

                            if (!string.IsNullOrWhiteSpace(info.Version))
                            {
                                Channel = next;
                                App.Settings.Prop.Channel = next;
                                App.Settings.Save();

                                ClientVersionCache.TryAdd($"{next}-{BinaryType}", info);

                                App.Logger.WriteLine(logIdent,
                                    $"Switched to working channel: {next}");

                                return info;
                            }
                        }
                        catch (HttpRequestException innerEx)
                            when (BadChannelCodes.Contains(innerEx.StatusCode))
                        {
                            continue;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                App.Logger.WriteLine(logIdent,
                    "All cycle channels failed. Falling back to production.");

                var fallback = await SafeGetJson<ClientVersion>(GetInfoUrl(DefaultChannel));
                Channel = DefaultChannel;
                App.Settings.Prop.Channel = DefaultChannel;
                App.Settings.Save();

                ClientVersionCache.TryAdd($"{DefaultChannel}-{BinaryType}", fallback);
                return fallback;
            }
        }

        public static async Task<Exception?> InitializeConnectivity()
        {
            const string logIdent = "Deployment::InitializeConnectivity";

            foreach (var entry in BaseUrls.OrderBy(x => x.Value))
            {
                try
                {
                    using var resp = await SharedHttp.GetAsync($"{entry.Key}/versionStudio");
                    if (!resp.IsSuccessStatusCode)
                        continue;

                    string content = (await resp.Content.ReadAsStringAsync()).Trim();
                    if (content == VersionStudioHash)
                    {
                        BaseUrl = entry.Key;
                        App.Logger.WriteLine(logIdent, $"Using base URL: {BaseUrl}");
                        return null;
                    }
                }
                catch { }
            }

            return new Exception("Failed to connect to any setup mirrors.");
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

        // redid this shit :3
        public static async Task<
            (string luaPackagesZip,
             string extraTexturesZip,
             string contentTexturesZip,
             string versionHash,
             string version)>
            DownloadForModGenerator(bool overwrite = false)
        {
            const string LOG_IDENT = "Deployment::DownloadForModGenerator";

            var clientInfo = await SafeGetJson<ClientVersion>(
                "https://clientsettingscdn.roblox.com/v2/client-version/WindowsStudio64");

            if (string.IsNullOrEmpty(clientInfo.VersionGuid) ||
                !clientInfo.VersionGuid.StartsWith("version-"))
                throw new InvalidHTTPResponseException("Invalid clientVersionUpload.");

            string versionHash = clientInfo.VersionGuid["version-".Length..];
            string version = clientInfo.Version;

            string tmp = Path.Combine(Path.GetTempPath(), "Voidstrap");
            Directory.CreateDirectory(tmp);

            string luaPackagesUrl =
                $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-luapackages.zip";
            string extraTexturesUrl =
                $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-textures.zip";
            string contentTexturesUrl =
                $"https://setup.rbxcdn.com/version-{versionHash}-content-textures2.zip";

            string luaPackagesZip =
                Path.Combine(tmp, $"extracontent-luapackages-{versionHash}.zip");
            string extraTexturesZip =
                Path.Combine(tmp, $"extracontent-textures-{versionHash}.zip");
            string contentTexturesZip =
                Path.Combine(tmp, $"content-textures2-{versionHash}.zip");

            async Task<string> DownloadFile(string url, string path)
            {
                if (File.Exists(path) && !overwrite)
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > 0)
                        return path;

                    File.Delete(path);
                }

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(5)
                };

                using var resp = await client.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead);

                resp.EnsureSuccessStatusCode();

                await using var fs =
                    new FileStream(path, FileMode.Create,
                        FileAccess.Write, FileShare.None);

                await resp.Content.CopyToAsync(fs);

                return path;
            }

            luaPackagesZip = await DownloadFile(luaPackagesUrl, luaPackagesZip);
            extraTexturesZip = await DownloadFile(extraTexturesUrl, extraTexturesZip);
            contentTexturesZip = await DownloadFile(contentTexturesUrl, contentTexturesZip);

            return (luaPackagesZip,
                    extraTexturesZip,
                    contentTexturesZip,
                    versionHash,
                    version);
        }
    }
}