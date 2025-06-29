using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Voidstrap.Resources;
using Voidstrap.UI.Elements.Base;

namespace Voidstrap.UI.Elements.Dialogs
{
    public partial class AddFastFlagDialog : WpfUiWindow
    {
        // Define flags that are not allowed to be imported
        private readonly string[] forbiddenFlags = new[] { "DFFlagNoMinimumSwimVelocity" };

        public MessageBoxResult Result = MessageBoxResult.Cancel;

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

            if (dialog.ShowDialog() != true)
                return;

            string fileContent = File.ReadAllText(dialog.FileName);

            // Check for forbidden flags before assigning to JsonTextBox
            if (ContainsForbiddenFlags(fileContent))
            {
                MessageBox.Show("The imported file contains forbidden flags and cannot be imported.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            JsonTextBox.Text = fileContent;
        }

        private void PresetValuesButton_Click(object sender, RoutedEventArgs e)
        {
            var presetDialog = new FFlagPresetsDialog();
            if (presetDialog.ShowDialog() == true && !string.IsNullOrEmpty(presetDialog.SelectedValue))
            {
                FlagValueTextBox.Text = presetDialog.SelectedValue;
            }
        }

        private bool ContainsForbiddenFlags(string jsonText)
        {
            try
            {
                // Parse JSON content
                var json = JToken.Parse(jsonText);

                // Recursively check all tokens for forbidden flags
                return CheckTokenForForbiddenFlags(json);
            }
            catch
            {
                // Block import on invalid JSON
                MessageBox.Show("Invalid JSON file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return true;  // treat as forbidden
            }
        }

        private bool CheckTokenForForbiddenFlags(JToken token)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                foreach (var prop in token.Children<JProperty>())
                {
                    if (IsForbiddenFlag(prop.Name) || IsForbiddenFlag(prop.Value.ToString()))
                    {
                        MessageBox.Show($"Forbidden flag found: Key='{prop.Name}' or Value='{prop.Value}'");
                        return true;
                    }

                    if (CheckTokenForForbiddenFlags(prop.Value))
                        return true;
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token.Children())
                {
                    if (CheckTokenForForbiddenFlags(item))
                        return true;
                }
            }
            else
            {
                if (IsForbiddenFlag(token.ToString()))
                {
                    MessageBox.Show($"Forbidden flag found in token: {token}");
                    return true;
                }
            }

            return false;
        }

        private bool IsForbiddenFlag(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            foreach (var flag in forbiddenFlags)
            {
                if (value.IndexOf(flag, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
