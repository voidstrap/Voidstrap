using Microsoft.Win32;
using System.Windows;
using Hellstrap.Resources;

namespace Hellstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddFastFlagDialog.xaml
    /// </summary>
    public partial class AddFastFlagDialog
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

            JsonTextBox.Text = File.ReadAllText(dialog.FileName);
        }



        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}