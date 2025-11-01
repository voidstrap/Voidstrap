using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Voidstrap.Enums;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    public class BetterBloxDataCenterConsoleViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<DataCenterModel> DatacenterCollection { get; } = new();
        public string ErrorMessage { get; private set; } = "";
        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;
        public BetterBloxDataCenterConsoleViewModel()
        {
            _ = LoadDatacentersAsync();
        }

        private async Task LoadDatacentersAsync()
        {
            try
            {
                using var http = new HttpClient();
                var json = await http.GetStringAsync("https://api.betterroblox.com/servers/datacenters");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                DatacenterCollection.Clear();

                foreach (var item in root.EnumerateObject())
                {
                    var datacenter = item.Value;
                    var location = datacenter.GetProperty("location");

                    DatacenterCollection.Add(new DataCenterModel
                    {
                        Id = datacenter.GetProperty("id").GetInt32(),
                        Datacenter = location.GetProperty("datacenter").GetString() ?? "Unknown",
                        City = location.GetProperty("city").GetString() ?? "Unknown",
                        Region = location.GetProperty("region").GetString() ?? "Unknown",
                        Country = location.GetProperty("country").GetString() ?? "Unknown",
                        Organization = datacenter.GetProperty("organization").GetString() ?? "Unknown"
                    });
                }
                LoadState = GenericTriState.Successful;
                OnPropertyChanged(nameof(DatacenterCollection));
                OnPropertyChanged(nameof(LoadState));
            }
            catch (Exception ex)
            {
                LoadState = GenericTriState.Failed;
                ErrorMessage = $"Error loading datacenters: {ex.Message}";
                OnPropertyChanged(nameof(LoadState));
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
    }

    public class DataCenterModel
    {
        public int Id { get; set; }
        public string Datacenter { get; set; } = "";
        public string City { get; set; } = "";
        public string Region { get; set; } = "";
        public string Country { get; set; } = "";
        public string Organization { get; set; } = "";
    }
}
