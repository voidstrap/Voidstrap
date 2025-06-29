using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Add this for KeyEventArgs and Key
using Microsoft.Win32;
using Voidstrap.UI.Elements.Base;

namespace Voidstrap.UI.Elements.Dialogs
{
    public partial class FFlagSearchDialog : WpfUiWindow
    {
        private readonly ObservableCollection<FlagSearchResult> _searchResults = new();
        private readonly ObservableCollection<FlagValidationResult> _validationResults = new();
        private readonly ObservableCollection<FlagSearchResult> _recentFlags = new();
        private readonly ObservableCollection<DataSourceInfo> _dataSources = new();
        
        private Dictionary<string, object> _allFlags = new();
        private Dictionary<string, FlagMetadata> _flagMetadata = new();
        private readonly HttpClient _httpClient = new();
        
        public FFlagSearchDialog()
        {
            InitializeComponent();
            InitializeDataSources();
            SetupDataGrids();
            _ = LoadDataAsync(); // Fire and forget - don't await in constructor
        }

        private void InitializeDataSources()
        {
            var sources = new[]
            {
                new DataSourceInfo { Name = "DynamicFastFlag", Url = "https://raw.githubusercontent.com/DynamicFastFlag/DynamicFastFlag/refs/heads/main/FvaribleV2.json", Status = "Pending" },
                new DataSourceInfo { Name = "PCClientBootstrapper", Url = "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/refs/heads/main/PCClientBootstrapper.json", Status = "Pending" },
                new DataSourceInfo { Name = "PCStudioApp", Url = "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/refs/heads/main/PCStudioApp.json", Status = "Pending" },
                new DataSourceInfo { Name = "PCDesktopClient", Url = "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/refs/heads/main/PCDesktopClient", Status = "Pending" },
                new DataSourceInfo { Name = "FVariables.txt", Url = "https://raw.githubusercontent.com/MaximumADHD/Roblox-Client-Tracker/refs/heads/roblox/FVariables.txt", Status = "Pending" },
                new DataSourceInfo { Name = "Froststap PCDesktopClient", Url = "https://raw.githubusercontent.com/SCR00M/froststap-shi/refs/heads/main/PCDesktopClient.json", Status = "Pending" },
                new DataSourceInfo { Name = "Froststap FVariables", Url = "https://raw.githubusercontent.com/SCR00M/froststap-shi/refs/heads/main/FVariablesV2.json", Status = "Pending" },
                new DataSourceInfo { Name = "Roblox ClientSettings", Url = "https://clientsettings.roblox.com/v2/settings/application/PCDesktopClient", Status = "Pending" }
            };

            foreach (var source in sources)
            {
                _dataSources.Add(source);
            }
        }

        private void SetupDataGrids()
        {
            SearchResultsDataGrid.ItemsSource = _searchResults;
            ValidationResultsDataGrid.ItemsSource = _validationResults;
            RecentFlagsDataGrid.ItemsSource = _recentFlags;
            // DataSourcesDataGrid.ItemsSource = _dataSources; // Comment out since we removed the tab
        }

        private async Task LoadDataAsync()
        {
            await UpdateStatusAsync("Loading flags...");
            ShowProgress(true);

            try
            {
                var allFlags = new Dictionary<string, object>();
                var flagMetadata = new Dictionary<string, FlagMetadata>();

                foreach (var source in _dataSources)
                {
                    try
                    {
                        source.Status = "Loading...";

                        var flags = await FetchFlagsFromSourceAsync(source.Url, source.Name);
                        
                        foreach (var flag in flags)
                        {
                            if (!allFlags.ContainsKey(flag.Key))
                            {
                                allFlags[flag.Key] = flag.Value;
                                flagMetadata[flag.Key] = new FlagMetadata 
                                { 
                                    Source = source.Name, 
                                    DateAdded = DateTime.Now 
                                };
                            }
                        }

                        source.Status = "✓ Success";
                        source.FlagCount = flags.Count;
                        source.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                    }
                    catch (Exception ex)
                    {
                        source.Status = "❌ Error";
                        App.Logger.WriteException($"FFlagSearch", ex);
                    }
                }

                _allFlags = allFlags;
                _flagMetadata = flagMetadata;

                await UpdateStatusAsync($"Done: {allFlags.Count} flags loaded!");
                UpdateTotalFlagsCount();
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync("Error loading flag data");
                App.Logger.WriteException("FFlagSearch", ex);
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async Task<Dictionary<string, object>> FetchFlagsFromSourceAsync(string url, string sourceName)
        {
            var flags = new Dictionary<string, object>();
            string response = string.Empty; // Declare response outside try block
            
            try
            {
                response = await _httpClient.GetStringAsync(url);

                // Try to parse as JSON first
                if (url.EndsWith(".json") || url.Contains("clientsettings.roblox.com"))
                {
                    var jsonDoc = JsonDocument.Parse(response);
                    
                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in jsonDoc.RootElement.EnumerateObject())
                        {
                            flags[property.Name] = property.Value.ValueKind switch
                            {
                                JsonValueKind.String => property.Value.GetString() ?? "",
                                JsonValueKind.Number => property.Value.TryGetInt32(out var intVal) ? intVal : property.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => property.Value.GetRawText()
                            };
                        }
                    }
                }
                else if (url.EndsWith(".txt"))
                {
                    // Parse text format (key=value pairs)
                    var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            
                            // Try to parse as appropriate type
                            if (bool.TryParse(value, out var boolVal))
                                flags[key] = boolVal;
                            else if (int.TryParse(value, out var intVal))
                                flags[key] = intVal;
                            else if (double.TryParse(value, out var doubleVal))
                                flags[key] = doubleVal;
                            else
                                flags[key] = value;
                        }
                    }
                }
                else
                {
                    // Try JSON anyway for other endpoints
                    var jsonDoc = JsonDocument.Parse(response);
                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        flags[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString() ?? "",
                            JsonValueKind.Number => property.Value.TryGetInt32(out var intVal) ? intVal : property.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => property.Value.GetRawText()
                        };
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, try text format
                // Now response is accessible since it's declared outside the try block
                if (!string.IsNullOrEmpty(response))
                {
                    var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            flags[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Log HTTP errors but don't crash - some sources may be unavailable
                App.Logger.WriteLine("FFlagSearch", $"Failed to fetch from {sourceName}: {ex.Message}");
                throw; // Re-throw to update status
            }

            return flags;
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchTerm = SearchTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                _searchResults.Clear();
                UpdateSearchResultsCount();
                return;
            }

            await Task.Delay(300); // Debounce
            if (SearchTextBox.Text?.Trim() != searchTerm) return; // User kept typing

            await PerformSearchAsync(searchTerm);
        }

        private async Task PerformSearchAsync(string searchTerm)
        {
            // Get filter states on UI thread BEFORE starting background task
            bool trueFlagsOnly = false;
            bool falseFlagsOnly = false;
            
            await Dispatcher.InvokeAsync(() =>
            {
                trueFlagsOnly = TrueFlagsOnlyCheckBox.IsChecked == true;
                falseFlagsOnly = FalseFlagsOnlyCheckBox.IsChecked == true;
            });

            await Task.Run(() =>
            {
                var results = new List<FlagSearchResult>();

                foreach (var flag in _allFlags)
                {
                    if (flag.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        var metadata = _flagMetadata.TryGetValue(flag.Key, out var meta) ? meta : new FlagMetadata();
                        
                        // Apply filters using the values captured on UI thread
                        if (trueFlagsOnly && !IsTrueValue(flag.Value))
                            continue;
                        if (falseFlagsOnly && !IsFalseValue(flag.Value))
                            continue;

                        results.Add(new FlagSearchResult
                        {
                            Name = flag.Key,
                            Value = flag.Value?.ToString() ?? "null",
                            Source = metadata.Source ?? "Unknown"
                        });
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    _searchResults.Clear();
                    foreach (var result in results.Take(1000)) // Limit to 1000 for performance
                    {
                        _searchResults.Add(result);
                    }
                    
                    UpdateSearchResultsCount();
                    ExportSearchResultsButton.IsEnabled = results.Any();
                    
                    if (results.Count > 1000)
                    {
                        StatusText.Text = $"Showing first 1000 of {results.Count} results. Use export to get all results.";
                    }
                });
            });
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            var input = ValidationInputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Please enter flags to validate.", "No Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ValidateFlagsAsync(input);
        }

        private async Task ValidateFlagsAsync(string input)
        {
            await UpdateStatusAsync("Validating flags...");
            ShowProgress(true);

            try
            {
                var results = new List<FlagValidationResult>();
                var inputFlags = new Dictionary<string, object>();

                // Try to parse as JSON
                try
                {
                    var jsonDoc = JsonDocument.Parse(input);
                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        inputFlags[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString() ?? "",
                            JsonValueKind.Number => property.Value.TryGetInt32(out var intVal) ? intVal : property.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => property.Value.GetRawText()
                        };
                    }
                }
                catch (JsonException)
                {
                    // Try line-by-line format
                    var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            inputFlags[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                // Check for duplicates in input
                var duplicates = inputFlags.GroupBy(x => x.Key).Where(g => g.Count() > 1).Select(g => g.Key);
                if (duplicates.Any())
                {
                    MessageBox.Show($"Duplicate flags found in input: {string.Join(", ", duplicates)}", 
                                  "Duplicates Detected", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Validate each flag
                foreach (var inputFlag in inputFlags)
                {
                    var result = new FlagValidationResult
                    {
                        Name = inputFlag.Key,
                        InputValue = inputFlag.Value?.ToString() ?? "null"
                    };

                    if (_allFlags.TryGetValue(inputFlag.Key, out var validValue))
                    {
                        result.Status = "✓ Valid";
                        result.ValidValue = validValue?.ToString() ?? "null";
                        result.Notes = "Flag exists in database";
                    }
                    else
                    {
                        result.Status = "❌ Invalid";
                        result.ValidValue = "N/A";
                        result.Notes = "Flag not found in any data source";
                    }

                    results.Add(result);
                }

                _validationResults.Clear();
                foreach (var result in results)
                {
                    _validationResults.Add(result);
                }

                UpdateValidationResultsCount();
                ExportValidResultsButton.IsEnabled = results.Any(r => r.Status == "✓ Valid");
                
                await UpdateStatusAsync($"Validated {results.Count} flags. {results.Count(r => r.Status == "✓ Valid")} valid, {results.Count(r => r.Status == "❌ Invalid")} invalid.");
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync("Error validating flags");
                MessageBox.Show($"Error validating flags: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async void FetchRecentButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateStatusAsync("Fetching recent flags...");
            ShowProgress(true);

            try
            {
                // For demonstration, we'll consider all flags as potentially recent
                // In a real implementation, you'd compare against a previous snapshot
                var recentFlags = _allFlags.Take(100).Select(flag => new FlagSearchResult
                {
                    Name = flag.Key,
                    Value = flag.Value?.ToString() ?? "null",
                    Source = _flagMetadata.TryGetValue(flag.Key, out var meta) ? meta.Source : "Unknown",
                    DateAdded = DateTime.Now.AddHours(-new Random().Next(0, 24)).ToString("yyyy-MM-dd HH:mm")
                }).ToList();

                _recentFlags.Clear();
                foreach (var flag in recentFlags)
                {
                    _recentFlags.Add(flag);
                }

                UpdateRecentFlagsCount();
                DownloadAllRecentButton.IsEnabled = recentFlags.Any();
                DownloadTrueRecentButton.IsEnabled = recentFlags.Any();
                DownloadFalseRecentButton.IsEnabled = recentFlags.Any();

                await UpdateStatusAsync($"Found {recentFlags.Count} recent flags");
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync("Error fetching recent flags");
                App.Logger.WriteException("FFlagSearch", ex);
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select flag file to validate"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(dialog.FileName);
                    ValidationInputTextBox.Text = content;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearValidationButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationInputTextBox.Clear();
            _validationResults.Clear();
            UpdateValidationResultsCount();
            ExportValidResultsButton.IsEnabled = false;
        }

        private async void ExportSearchResultsButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportFlagsAsync(_searchResults.ToDictionary(r => r.Name, r => ParseValue(r.Value)), "search_results");
        }

        private async void ExportValidResultsButton_Click(object sender, RoutedEventArgs e)
        {
            var validFlags = _validationResults.Where(r => r.Status == "✓ Valid")
                                               .ToDictionary(r => r.Name, r => ParseValue(r.ValidValue));
            await ExportFlagsAsync(validFlags, "valid_flags");
        }

        private async void DownloadAllRecentButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportFlagsAsync(_recentFlags.ToDictionary(r => r.Name, r => ParseValue(r.Value)), "recent_flags_all");
        }

        private async void DownloadTrueRecentButton_Click(object sender, RoutedEventArgs e)
        {
            var trueFlags = _recentFlags.Where(r => IsTrueValue(ParseValue(r.Value)))
                                       .ToDictionary(r => r.Name, r => ParseValue(r.Value));
            await ExportFlagsAsync(trueFlags, "recent_flags_true");
        }

        private async void DownloadFalseRecentButton_Click(object sender, RoutedEventArgs e)
        {
            var falseFlags = _recentFlags.Where(r => IsFalseValue(ParseValue(r.Value)))
                                        .ToDictionary(r => r.Name, r => ParseValue(r.Value));
            await ExportFlagsAsync(falseFlags, "recent_flags_false");
        }

        private async Task ExportFlagsAsync(Dictionary<string, object> flags, string defaultName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var json = JsonSerializer.Serialize(flags, options);
                    await File.WriteAllTextAsync(dialog.FileName, json);
                    
                    MessageBox.Show($"Exported {flags.Count} flags to {dialog.FileName}", "Export Complete", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting flags: {ex.Message}", "Export Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Helper methods
        private async Task UpdateStatusAsync(string status)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = status);
        }

        private void ShowProgress(bool show)
        {
            Dispatcher.Invoke(() => LoadingProgress.Visibility = show ? Visibility.Visible : Visibility.Collapsed);
        }

        private void UpdateSearchResultsCount()
        {
            SearchResultsCount.Text = $"{_searchResults.Count} results";
        }

        private void UpdateValidationResultsCount()
        {
            ValidationResultsCount.Text = $"{_validationResults.Count} results";
        }

        private void UpdateRecentFlagsCount()
        {
            RecentFlagsCount.Text = $"{_recentFlags.Count} recent flags";
        }

        private void UpdateTotalFlagsCount()
        {
            // Since we removed the TotalFlagsCount TextBlock, just update the status
            if (_allFlags.Count > 0)
            {
                StatusText.Text = $"Done: {_allFlags.Count} flags loaded!";
            }
            
            // Remove or comment out this line since TotalFlagsCount doesn't exist:
            // TotalFlagsCount.Text = $"Total flags: {_allFlags.Count}";
        }

        private static bool IsTrueValue(object value)
        {
            return value switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                int i => i != 0,
                _ => false
            };
        }

        private static bool IsFalseValue(object value)
        {
            return value switch
            {
                bool b => !b,
                string s => s.Equals("false", StringComparison.OrdinalIgnoreCase),
                int i => i == 0,
                _ => false
            };
        }

        private static object ParseValue(string value)
        {
            if (bool.TryParse(value, out var boolVal))
                return boolVal;
            if (int.TryParse(value, out var intVal))
                return intVal;
            if (double.TryParse(value, out var doubleVal))
                return doubleVal;
            return value;
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }

        private void TrueFlagsOnlyCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Re-run search with current term when filter changes
            var searchTerm = SearchTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                _ = PerformSearchAsync(searchTerm);
            }
        }

        private void FalseFlagsOnlyCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Re-run search with current term when filter changes
            var searchTerm = SearchTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                _ = PerformSearchAsync(searchTerm);
            }
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    
                    // Use the standard WPF paste operation by simulating it
                    ValidationInputTextBox.Focus();
                    
                    // Send Ctrl+V key combination to the focused text box
                    // This allows the TextBox to handle the paste operation natively
                    var pasteCommand = ApplicationCommands.Paste;
                    if (pasteCommand.CanExecute(null, ValidationInputTextBox))
                    {
                        pasteCommand.Execute(null, ValidationInputTextBox);
                    }
                    else
                    {
                        // Fallback: direct assignment if command doesn't work
                        ValidationInputTextBox.Text = clipboardText;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pasting from clipboard: {ex.Message}", "Paste Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ValidationInputTextBox.Clear();
        }

        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ValidationInputTextBox.SelectAll();
        }

        private void SampleFormatButton_Click(object sender, RoutedEventArgs e)
        {
            string sampleJson = @"{
  ""FFlagDebugDisplayFPS"": ""True"",
  ""DFIntTaskSchedulerTargetFps"": ""120"",
  ""FFlagDisablePostFx"": ""False"",
  ""DFIntRenderClampRoughnessMax"": ""-640000000""
}";
    
            ValidationInputTextBox.Text = sampleJson;
        }
    }

    // Data models
    public class FlagSearchResult : INotifyPropertyChanged
    {
        private string _name = "";
        private string _value = "";
        private string _source = "";
        private string _dateAdded = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged(); }
        }

        public string DateAdded
        {
            get => _dateAdded;
            set { _dateAdded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FlagValidationResult : INotifyPropertyChanged
    {
        private string _name = "";
        private string _inputValue = "";
        private string _status = "";
        private string _validValue = "";
        private string _notes = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string InputValue
        {
            get => _inputValue;
            set { _inputValue = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string ValidValue
        {
            get => _validValue;
            set { _validValue = value; OnPropertyChanged(); }
        }

        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DataSourceInfo : INotifyPropertyChanged
    {
        private string _name = "";
        private string _url = "";
        private string _status = "";
        private int _flagCount;
        private string _lastUpdated = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public int FlagCount
        {
            get => _flagCount;
            set { _flagCount = value; OnPropertyChanged(); }
        }

        public string LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FlagMetadata
    {
        public string Source { get; set; } = "";
        public DateTime DateAdded { get; set; }
    }
}