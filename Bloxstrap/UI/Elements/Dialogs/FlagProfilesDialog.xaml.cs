using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hellstrap.Resources;

namespace Hellstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for FlagProfilesDialog.xaml
    /// </summary>
    public partial class FlagProfilesDialog
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public FlagProfilesDialog()
        {
            InitializeComponent();
            LoadBackups();
        }

        private void LoadBackups()
        {
            LoadBackup.Items.Clear();

            var backupsDirectory = Path.Combine(Paths.Base, Paths.SavedBackups);

            try
            {
                if (!Directory.Exists(backupsDirectory))
                {
                    Directory.CreateDirectory(backupsDirectory);
                }

                foreach (var profilePath in Directory.GetFiles(backupsDirectory))
                {
                    var profileName = Path.GetFileName(profilePath);
                    if (!string.IsNullOrWhiteSpace(profileName))
                    {
                        LoadBackup.Items.Add(profileName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading backups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadBackup.SelectedItem is string selectedProfileName && !string.IsNullOrWhiteSpace(selectedProfileName))
            {
                App.FastFlags.DeleteBackup(selectedProfileName);
                LoadBackups();
            }
        }

        // Prevent the window from closing if the user clicks the close button (X)
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // Cancel the close operation
            // Optionally, you can show a message here to indicate that the dialog won't close
        }
    }
}
