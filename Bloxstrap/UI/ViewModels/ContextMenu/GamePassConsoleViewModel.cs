using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Enums;
using Voidstrap.Integrations;
using System.Text.Json;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal partial class GamePassConsoleViewModel : NotifyPropertyChangedViewModel
    {
        private readonly HttpClient _httpClient = new();

        public ObservableCollection<GamePassData> GamePassesCollection { get; } = new();

        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;

        public string ErrorMessage { get; private set; } = string.Empty;

        public ICommand CloseWindowCommand => new RelayCommand(RequestClose);

        public EventHandler? RequestCloseEvent;

        public ICommand LoadGamePassesCommand { get; }

        public GamePassConsoleViewModel()
        {
            LoadGamePassesCommand = new AsyncRelayCommand<long>(LoadGamePassesAsync);
        }

        private async Task LoadGamePassesAsync(long userId)
        {
            LoadState = GenericTriState.Unknown;
            OnPropertyChanged(nameof(LoadState));

            try
            {
                ErrorMessage = string.Empty;
                GamePassesCollection.Clear();

                var url = $"https://apis.roblox.com/game-passes/v1/users/{userId}/game-passes?count=101";
                var result = await _httpClient.GetFromJsonAsync<GamePassResponse>(url);

                if (result?.GamePasses == null || result.GamePasses.Count == 0)
                {
                    ErrorMessage = "No gamepasses found for this user.";
                    LoadState = GenericTriState.Failed;
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(LoadState));
                    return;
                }

                foreach (var gp in result.GamePasses)
                {
                    if (string.IsNullOrWhiteSpace(gp.Description))
                        gp.Description = "No description";

                    if (!gp.IsForSale || gp.Price is null)
                        gp.DisplayPrice = "Not for sale";
                    else
                        gp.DisplayPrice = $"{gp.Price}";

                    gp.IconUrl = await GetThumbnailUrlAsync(gp.IconAssetId);
                    GamePassesCollection.Add(gp);
                }

                LoadState = GenericTriState.Successful;
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"Failed to fetch gamepasses: {ex.Message}";
                LoadState = GenericTriState.Failed;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                LoadState = GenericTriState.Failed;
            }
            finally
            {
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(LoadState));
                OnPropertyChanged(nameof(GamePassesCollection));
            }
        }

        private async Task<string> GetThumbnailUrlAsync(long assetId)
        {
            try
            {
                if (assetId <= 0)
                    return "https://tr.rbxcdn.com/6b05f76e9c083963b25a7e4216c86c93/150/150/Image/Png";

                var thumbUrl = $"https://thumbnails.roblox.com/v1/assets?assetIds={assetId}&size=150x150&format=Png&isCircular=false";
                using var response = await _httpClient.GetAsync(thumbUrl);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                var imageUrl = doc.RootElement
                    .GetProperty("data")[0]
                    .GetProperty("imageUrl")
                    .GetString();

                return string.IsNullOrWhiteSpace(imageUrl)
                    ? "https://tr.rbxcdn.com/6b05f76e9c083963b25a7e4216c86c93/150/150/Image/Png"
                    : imageUrl;
            }
            catch
            {
                return "https://tr.rbxcdn.com/6b05f76e9c083963b25a7e4216c86c93/150/150/Image/Png";
            }
        }

        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);
    }

    internal class GamePassResponse
    {
        public List<GamePassData> GamePasses { get; set; } = new();
    }

    internal class GamePassData
    {
        public long GamePassId { get; set; }
        public long IconAssetId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsForSale { get; set; }
        public int? Price { get; set; }
        public GamePassCreator Creator { get; set; } = new();

        public string IconUrl { get; set; } = string.Empty;
        public string DisplayPrice { get; set; } = string.Empty;
        public string CreatorName => Creator?.Name ?? "Unknown";
    }

    internal class GamePassCreator
    {
        public string CreatorType { get; set; } = string.Empty;
        public long CreatorId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
