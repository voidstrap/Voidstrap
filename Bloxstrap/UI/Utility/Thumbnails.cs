using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Voidstrap;

namespace Voidstrap.Utility
{
    internal static class Thumbnails
    {
        private const int RETRIES = 5;
        private const int RETRY_TIME_INCREMENT = 500;

        /// <remarks>
        /// Returned array may contain null values
        /// </remarks>
        public static async Task<string?[]> GetThumbnailUrlsAsync(List<ThumbnailRequest> requests, CancellationToken token)
        {
            const string LOG_IDENT = "Thumbnails::GetThumbnailUrlsAsync";

            string?[] urls = new string?[requests.Count];
            for (int i = 0; i < requests.Count; i++)
                requests[i].RequestId = i.ToString();

            ThumbnailResponse[] response = null!;

            for (int attempt = 1; attempt <= RETRIES; attempt++)
            {
                var json = await PostWithRetriesAsync<ThumbnailBatchResponse>("https://thumbnails.roblox.com/v1/batch", requests, token);

                if (json == null)
                    throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                response = json.Data;

                bool finished = response.All(x => x.State != "Pending");
                if (finished)
                    break;

                if (attempt == RETRIES)
                    App.Logger.WriteLine(LOG_IDENT, "Ran out of retries");
                else
                    await Task.Delay(RETRY_TIME_INCREMENT * attempt, token);
            }

            foreach (var item in response)
            {
                if (item.State == "Pending")
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} is still pending");
                else if (item.State == "Error")
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got error code {item.ErrorCode} ({item.ErrorMessage})");
                else if (item.State != "Completed")
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got \"{item.State}\"");

                urls[int.Parse(item.RequestId)] = item.ImageUrl;
            }

            return urls;
        }

        public static async Task<string?> GetThumbnailUrlAsync(ThumbnailRequest request, CancellationToken token)
        {
            const string LOG_IDENT = "Thumbnails::GetThumbnailUrlAsync";

            request.RequestId = "0";

            ThumbnailResponse response = null!;

            for (int attempt = 1; attempt <= RETRIES; attempt++)
            {
                var json = await PostWithRetriesAsync<ThumbnailBatchResponse>("https://thumbnails.roblox.com/v1/batch", new[] { request }, token);

                if (json == null)
                    throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                response = json.Data[0];

                if (response.State != "Pending")
                    break;

                if (attempt == RETRIES)
                    App.Logger.WriteLine(LOG_IDENT, "Ran out of retries");
                else
                    await Task.Delay(RETRY_TIME_INCREMENT * attempt, token);
            }

            if (response.State == "Pending")
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} is still pending");
            else if (response.State == "Error")
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} got error code {response.ErrorCode} ({response.ErrorMessage})");
            else if (response.State != "Completed")
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} got \"{response.State}\"");

            return response.ImageUrl;
        }
        private static async Task<T?> PostWithRetriesAsync<T>(string url, object payload, CancellationToken token, int retries = 3)
        {
            HttpResponseMessage? response = null;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    response = await App.HttpClient.PostAsJsonAsync(url, payload, token);

                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: token);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Thumbnails::PostWithRetriesAsync", $"Attempt {attempt} failed: {ex.Message}");
                }

                await Task.Delay(RETRY_TIME_INCREMENT * attempt, token);
            }

            return default;
        }
    }
}
