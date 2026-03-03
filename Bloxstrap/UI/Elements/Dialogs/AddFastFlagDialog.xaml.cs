using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Voidstrap.Resources;
using Voidstrap.UI.Elements.Base;

namespace Voidstrap.UI.Elements.Dialogs
{
    public class FastFlagItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<string> VisibleTags { get; set; } = new List<string>();
    }

    public partial class AddFastFlagDialog : WpfUiWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;
        public List<FastFlagItem> ImportedFlags { get; private set; } = new List<FastFlagItem>();

        public AddFastFlagDialog()
        {
            InitializeComponent();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles} (*.json;*.txt;*.md)|*.json;*.txt;*.md"
            };

            if (dialog.ShowDialog() != true) return;

            string fileContent = File.ReadAllText(dialog.FileName);
            JsonTextBox.Text = fileContent;

            ParseJsonToFlags(fileContent);
        }

        private void JsonTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParseJsonToFlags(JsonTextBox.Text);
        }

        private void ParseJsonToFlags(string json)
        {
            ImportedFlags.Clear();

            try
            {
                var dict = JObject.Parse(json)
                                  .Properties()
                                  .ToDictionary(p => p.Name, p => p.Value.ToString());

                ImportedFlags = dict.Select(kvp =>
                {
                    string tag = "Unknown";

                    if (kvp.Key.StartsWith("FFlag")) tag = "FFlag";
                    else if (kvp.Key.StartsWith("DFFlag")) tag = "DFFlag";
                    else if (kvp.Key.StartsWith("FInt")) tag = "FInt";
                    else if (kvp.Key.StartsWith("DFInt")) tag = "DFInt";
                    else if (kvp.Key.StartsWith("FString")) tag = "FString";
                    else if (kvp.Key.StartsWith("FDouble")) tag = "FDouble";

                    return new FastFlagItem
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        VisibleTags = new List<string> { tag }
                    };
                }).ToList();

                UpdateBase64Tab();
            }
            catch
            {
                // we CATCH nothin :3
            }
        }

        private void UpdateBase64Tab()
        {
            if (!ImportedFlags.Any()) return;

            var dict = ImportedFlags.ToDictionary(f => f.Name, f => f.Value);
            string jsonText = JObject.FromObject(dict).ToString();
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonText));
            Base64TextBox.Text = base64;
        }

        private void PasteBase64Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Base64TextBox.Text)) return;

            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Base64TextBox.Text));
                JsonTextBox.Text = decoded;
                Tabs.SelectedIndex = 1;
            }
            catch
            {
                Frontend.ShowMessageBox("Invalid Base64 string!");
            }
        }

        private void PresetValuesButton_Click(object sender, RoutedEventArgs e)
        {
            var presetDialog = new FFlagPresetsDialog();
            if (presetDialog.ShowDialog() == true && !string.IsNullOrEmpty(presetDialog.SelectedValue))
            {
                FlagValueTextBox.Text = presetDialog.SelectedValue;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}