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
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            LoadProfile.Items.Clear();

            var profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);

            try
            {
                if (!Directory.Exists(profilesDirectory))
                {
                    Directory.CreateDirectory(profilesDirectory);
                }

                foreach (var profilePath in Directory.GetFiles(profilesDirectory))
                {
                    var profileName = Path.GetFileName(profilePath);
                    if (!string.IsNullOrWhiteSpace(profileName))
                    {
                        LoadProfile.Items.Add(profileName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading profiles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is string selectedProfileName && !string.IsNullOrWhiteSpace(selectedProfileName))
            {
                App.FastFlags.DeleteProfile(selectedProfileName);
                LoadProfiles();
            }
        }
    }
}
