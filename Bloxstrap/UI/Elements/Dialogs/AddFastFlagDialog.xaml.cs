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

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
