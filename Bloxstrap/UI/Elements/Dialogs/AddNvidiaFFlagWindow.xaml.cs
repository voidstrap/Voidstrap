using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Voidstrap.Models;

namespace Voidstrap.UI.Elements.Dialogs
{
    public partial class AddNvidiaFFlagWindow// I think diddy coded thiss
    {
        public List<NvidiaEditorEntry> ResultEntries { get; } = new();

        public AddNvidiaFFlagWindow()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            ResultEntries.Clear();

            if (Tabs.SelectedIndex == 0)
            {
                if (!TryAddSingle())
                    return;
            }
            else if (Tabs.SelectedIndex == 2)
            {
                if (!TryAddFullValue())
                    return;
            }
            else
            {
                if (!TryImportNip())
                    return;
            }

            DialogResult = true;
            Close();
        }

        private bool TryAddSingle()
        {
            string name = NameBox.Text.Trim();
            string settingId = SettingIdBox.Text.Trim();
            string value = ValueBox.Text.Trim();
            string valueType =
                ((ComboBoxItem)ValueTypeBox.SelectedItem).Content.ToString()!;

            if (string.IsNullOrWhiteSpace(name))
            {
                Frontend.ShowMessageBox("Setting Name is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settingId))
            {
                Frontend.ShowMessageBox("Setting ID is required.");
                return false;
            }

            ResultEntries.Add(new NvidiaEditorEntry
            {
                Name = name,
                SettingId = settingId,
                Value = string.IsNullOrWhiteSpace(value) ? "0" : value,
                ValueType = NormalizeValueType(valueType)
            });

            return true;
        }

        private bool TryAddFullValue()
        {
            string fullValue = FullValueBox.Text.Trim();
            var parts = fullValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                Frontend.ShowMessageBox("Please enter the Data.");
                return false;
            }

            string name = string.Join(" ", parts.Take(parts.Length - 3));
            string settingId = parts[parts.Length - 3];
            string value = parts[parts.Length - 2];
            string valueType = parts[parts.Length - 1];

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(settingId))
            {
                Frontend.ShowMessageBox("Setting Name and Setting ID are required.");
                return false;
            }

            ResultEntries.Add(new NvidiaEditorEntry
            {
                Name = name,
                SettingId = settingId,
                Value = string.IsNullOrWhiteSpace(value) ? "0" : value,
                ValueType = NormalizeValueType(valueType)
            });

            return true;
        }

        private void ImportFromFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "NVIDIA Profile (*.nip)|*.nip",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            NipTextBox.Text = System.IO.File.ReadAllText(dialog.FileName);
            Tabs.SelectedIndex = 1;
        }

        private static string NormalizeValueType(string? type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "dword" => "Dword",
                "string" => "String",
                "binary" => "Binary",
                "boolean" => "Boolean",
                "bool" => "Boolean",
                "hex" => "Hex",
                _ => "Dword"
            };
        }

        private void ParseAndAdd_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedIndex == 2)
            {
                if (TryAddFullValue())
                {
                    // GOOFCORNBALL gulp bratic :3
                }
            }
            else
            {
                if (Tabs.SelectedIndex == 0)
                {
                    if (!TryAddSingle())
                        return;
                }
                else if (Tabs.SelectedIndex == 1)
                {
                    if (!TryImportNip())
                        return;
                }
            }

            DialogResult = true;
            Close();
        }

        private bool TryImportNip()
        {
            try
            {
                var doc = XDocument.Parse(NipTextBox.Text);

                var imported = doc
                    .Descendants()
                    .Where(x => x.Name.LocalName.Equals(
                        "ProfileSetting",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(x =>
                    {
                        string id =
                            x.Elements().FirstOrDefault(e =>
                                e.Name.LocalName.Equals(
                                    "SettingID",
                                    StringComparison.OrdinalIgnoreCase))
                            ?.Value?.Trim() ?? "";

                        if (string.IsNullOrWhiteSpace(id))
                            return null;

                        string name =
                            x.Elements().FirstOrDefault(e =>
                                e.Name.LocalName.Equals(
                                    "SettingNameInfo",
                                    StringComparison.OrdinalIgnoreCase))
                            ?.Value?.Trim() ?? "";

                        if (string.IsNullOrWhiteSpace(name))
                            return null;

                        string value =
                            x.Elements().FirstOrDefault(e =>
                                e.Name.LocalName.Equals(
                                    "SettingValue",
                                    StringComparison.OrdinalIgnoreCase))
                            ?.Value?.Trim() ?? "0";

                        string valueType =
                            NormalizeValueType(
                                x.Elements().FirstOrDefault(e =>
                                    e.Name.LocalName.Equals(
                                        "ValueType",
                                        StringComparison.OrdinalIgnoreCase))
                                ?.Value);

                        return new NvidiaEditorEntry
                        {
                            SettingId = id,
                            Name = name,
                            Value = value,
                            ValueType = valueType
                        };
                    })
                    .Where(x => x != null)
                    .DistinctBy(x => x!.SettingId)
                    .ToList()!;

                if (!imported.Any())
                {
                    Frontend.ShowMessageBox("No valid NVIDIA settings found.");
                    return false;
                }

                ResultEntries.AddRange(imported);
                return true;
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Invalid or corrupted NIP file:\n\n{ex.Message}");
                return false;
            }
        }
    }
}