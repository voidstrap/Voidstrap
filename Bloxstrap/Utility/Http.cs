using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Voidstrap.Utility
{
    internal static class Http
    {
        public static async Task<T> GetJson<T>(string url)
            => await GetJson<T>(url, CancellationToken.None);
        public static async Task<T> GetJson<T>(string url, CancellationToken token)
        {
            var request = await App.HttpClient.GetAsync(url, token);
            request.EnsureSuccessStatusCode();

            string json = await request.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<T>(json)!;
        }
        public static async Task<T?> PostJson<T>(string url, object body, CancellationToken token = default)
        {
            string jsonBody = JsonSerializer.Serialize(body);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await App.HttpClient.PostAsync(url, content, token);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<T>(json);
        }
        public static async Task<string> GetString(string url, CancellationToken token = default)
        {
            var response = await App.HttpClient.GetAsync(url, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(token);
        }
    }
}
