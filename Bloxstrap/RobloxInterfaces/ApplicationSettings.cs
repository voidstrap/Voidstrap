using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Voidstrap;

namespace Voidstrap.RobloxInterfaces
{
    public class ApplicationSettings
    {
        private readonly string _applicationName;
        private readonly string _channelName;
        private bool _initialised;
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

                string logIdent = $"ApplicationSettings::Fetch.{_applicationName}.{_channelName}";
                App.Logger.WriteLine(logIdent, "Fetching fast flags...");

                string path = $"/v2/settings/application/{_applicationName}";
                if (!string.Equals(_channelName, Deployment.DefaultChannel, StringComparison.OrdinalIgnoreCase))
                    path += $"/bucket/{_channelName}";

                HttpResponseMessage? response = null;
                string[] baseUrls =
                {
                    "https://clientsettingscdn.roblox.com",
                    "https://clientsettings.roblox.com",
                    "https://setup.rbxcdn.com"
                };

                Exception? lastError = null;
                foreach (var baseUrl in baseUrls)
                {
                    string url = baseUrl + path;

                    try
                    {
                        App.Logger.WriteLine(logIdent, $"Trying {url}");
                        response = await App.HttpClient.GetAsync(url).ConfigureAwait(false);

                        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                            response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (raw.Contains("bucket name is invalid", StringComparison.OrdinalIgnoreCase))
                            {
                                App.Logger.WriteLine(logIdent, $"Invalid bucket '{_channelName}'. Falling back to default channel...");
                                path = $"/v2/settings/application/{_applicationName}";
                                continue;
                            }
                        }

                        response.EnsureSuccessStatusCode();
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        lastError = ex;
                        App.Logger.WriteLine(logIdent, $"Error contacting {baseUrl}: {ex.Message}");
                    }
                }

                if (response == null)
                    throw new Exception("All configuration sources failed.", lastError);

                using (response)
                {
                    string rawResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(rawResponse))
                        throw new Exception("Empty response from configuration endpoint.");

                    var clientSettings = JsonSerializer.Deserialize<ClientFlagSettings>(rawResponse);

                    if (clientSettings?.ApplicationSettings == null)
                        throw new Exception("Deserialized ApplicationSettings is null!");

                    _flags = clientSettings.ApplicationSettings;
                    _initialised = true;

                    App.Logger.WriteLine(logIdent, $"Fetched {_flags.Count} fast flags successfully.");
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
                    return (T?)converter.ConvertFromInvariantString(value);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ApplicationSettings::GetAsync", ex);
            }

            return default;
        }

        public T? Get<T>(string name) =>
            GetAsync<T>(name).ConfigureAwait(false).GetAwaiter().GetResult();

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
