using System.Collections.Concurrent;
using Voidstrap.RobloxInterfaces;
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
                    cts.Cancel(); // Cancel others
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
                var useCommon = ApplicationSettings.GetSettings(nameof(ApplicationSettings.PCClientBootstrapper), Channel)
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

            var path = isDefault
                ? $"/v2/client-version/{BinaryType}"
                : $"/v2/client-version/{BinaryType}/channel/{channel}";

            ClientVersion clientVersion;

            try
            {
                clientVersion = await Http.GetJson<ClientVersion>($"https://clientsettingscdn.roblox.com{path}");
            }
            catch (HttpRequestException ex) when (!isDefault && BadChannelCodes.Contains(ex.StatusCode))
            {
                throw new InvalidChannelException(ex.StatusCode);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, "Fallback to clientsettings.roblox.com");
                App.Logger.WriteException(logIdent, ex);

                try
                {
                    clientVersion = await Http.GetJson<ClientVersion>($"https://clientsettings.roblox.com{path}");
                }
                catch (HttpRequestException ex2) when (!isDefault && BadChannelCodes.Contains(ex2.StatusCode))
                {
                    throw new InvalidChannelException(ex2.StatusCode);
                }
            }

            // Check version lag behind default
            if (!isDefault)
            {
                var defaultVer = await GetInfo(DefaultChannel);
                if (Utilities.CompareVersions(clientVersion.Version, defaultVer.Version) == VersionComparison.LessThan)
                    clientVersion.IsBehindDefaultChannel = true;
            }

            ClientVersionCache.TryAdd(cacheKey, clientVersion);
            return clientVersion;
        }
    }
}
