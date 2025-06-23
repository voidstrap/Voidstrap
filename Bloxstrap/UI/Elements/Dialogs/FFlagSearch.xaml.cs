using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Voidstrap.UI.Elements.Dialogs
{
    public partial class FFlagSearch
    {
        private static readonly HttpClient httpClient = new();

        private List<KeyValuePair<string, JsonElement>> allFlags = new();

        public FFlagSearch(Exception exception)
        {
            InitializeComponent();
            ShowMessage("Loading... (THIS CAN CAUSE CRASHES BIG FILE)");
            _ = LoadFlagsAsync();
        }

        private async Task LoadFlagsAsync()
        {
            try
            {
                const string url = "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/main/PCDesktopClient.json";
                string jsonContent = await httpClient.GetStringAsync(url).ConfigureAwait(false);

                using var document = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { AllowTrailingCommas = true });
                var root = document.RootElement;

                allFlags = root.EnumerateObject()
                               .Select(prop => new KeyValuePair<string, JsonElement>(prop.Name, prop.Value))
                               .OrderBy(kvp => kvp.Key)
                               .ToList();

                await Dispatcher.InvokeAsync(() => UpdateDisplayedFlags(allFlags));
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => ShowMessage($"Error loading FFlags: {ex.Message}"));
            }
        }

        private void ShowMessage(string message)
        {
            FFlags.Document.Blocks.Clear();
            FFlags.Document.Blocks.Add(new Paragraph(new Run(message)));
        }

        private void UpdateDisplayedFlags(List<KeyValuePair<string, JsonElement>> flags)
        {
            FFlags.Document.Blocks.Clear();

            if (flags.Count == 0)
            {
                ShowMessage("No flags found.");
                return;
            }

            var document = FFlags.Document;
            document.Blocks.Clear();

            foreach (var (key, value) in flags)
            {
                string valueString = GetReadableValue(value);

                var paragraph = new Paragraph
                {
                    Inlines =
                    {
                        new Run(key) { Foreground = Brushes.White },
                        new Run(": "),
                        new Run(valueString) { Foreground = Brushes.Blue }
                    }
                };

                document.Blocks.Add(paragraph);
            }
        }

        private static string GetReadableValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => $"\"{value.GetString().Replace("\"", "\\\"")}\"",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => "[Object]",
            JsonValueKind.Array => "[Array]",
            _ => value.GetRawText()
        };

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
