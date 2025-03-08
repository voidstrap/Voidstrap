using Hellstrap.Models.RobloxApi;

namespace Hellstrap.Models.Entities
{
    public class UserDetails
    {
        private static readonly List<UserDetails> _cache = new();
        private static readonly object _cacheLock = new();

        public GetUserResponse Data { get; private set; } = null!;
        public ThumbnailResponse Thumbnail { get; private set; } = null!;

        public static async Task<UserDetails> Fetch(long id)
        {
            lock (_cacheLock)
            {
                var cachedUser = _cache.FirstOrDefault(x => x.Data?.Id == id);
                if (cachedUser is not null)
                    return cachedUser;
            }

            var userResponse = await Http.GetJson<GetUserResponse>($"https://users.roblox.com/v1/users/{id}")
                ?? throw new InvalidHTTPResponseException($"Failed to fetch user details for ID {id}");

            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={id}&size=180x180&format=Png&isCircular=false");

            if (thumbnailResponse?.Data is null || !thumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException($"Failed to fetch avatar thumbnail for ID {id}");

            var details = new UserDetails
            {
                Data = userResponse,
                Thumbnail = thumbnailResponse.Data.First()
            };

            lock (_cacheLock)
            {
                _cache.Add(details);
            }

            return details;
        }
    }
}
