using System.ComponentModel;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Voidstrap;

namespace Voidstrap.RobloxInterfaces
{
    public class ApplicationSettings
    {
        private readonly string _applicationName;
        private readonly string _channelName;
        private bool _initialised = false;
        private Dictionary<string, string>? _flags;

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private ApplicationSettings(string applicationName, string channelName)
        {
            _applicationName = applicationName;
            _channelName = channelName;
        }

        private async Task FetchAsync()
        {
            if (_initialised)
                return;

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialised)
                    return;

                string logIndent = $"ApplicationSettings::Fetch.{_applicationName}.{_channelName}";
                App.Logger.WriteLine(logIndent, "Fetching fast flags");

                string path = $"/v2/settings/application/{_applicationName}";
                if (_channelName != Deployment.DefaultChannel.ToLowerInvariant())
                    path += $"/bucket/{_channelName}";

                HttpResponseMessage response;

                try
                {
                    response = await App.HttpClient.GetAsync("https://clientsettingscdn.roblox.com" + path).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(logIndent, "Failed to contact clientsettingscdn! Falling back to clientsettings...");
                    App.Logger.WriteException(logIndent, ex);

                    response = await App.HttpClient.GetAsync("https://clientsettings.roblox.com" + path).ConfigureAwait(false);
                }

                using (response)
                {
                    response.EnsureSuccessStatusCode();
                    string rawResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var clientSettings = JsonSerializer.Deserialize<ClientFlagSettings>(rawResponse);

                    if (clientSettings?.ApplicationSettings == null)
                        throw new Exception("Deserialized application settings is null!");

                    _flags = clientSettings.ApplicationSettings;
                    _initialised = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<T?> GetAsync<T>(string name)
        {
            await FetchAsync().ConfigureAwait(false);

            if (_flags == null || !_flags.TryGetValue(name, out string value))
                return default;

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return (T?)converter.ConvertFromInvariantString(value);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ApplicationSettings::GetAsync", ex);
            }

            return default;
        }

        // Remove sync-over-async: Get<T>() is discouraged
        // If absolutely needed, use responsibly (e.g., test app startup)
        public T? Get<T>(string name) => GetAsync<T>(name).ConfigureAwait(false).GetAwaiter().GetResult();

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Lazy<ApplicationSettings>>> _cache = new();

        public static ApplicationSettings PCDesktopClient => GetSettings("PCDesktopClient");
        public static ApplicationSettings PCClientBootstrapper => GetSettings("PCClientBootstrapper");

        public static ApplicationSettings GetSettings(string applicationName, string channelName = Deployment.DefaultChannel, bool shouldCache = true)
        {
            channelName = channelName.ToLowerInvariant();

            if (!shouldCache)
                return new ApplicationSettings(applicationName, channelName);

            var channelMap = _cache.GetOrAdd(applicationName, _ => new ConcurrentDictionary<string, Lazy<ApplicationSettings>>());
            return channelMap.GetOrAdd(channelName, _ => new Lazy<ApplicationSettings>(() =>
                new ApplicationSettings(applicationName, channelName))).Value;
        }
    }
}
