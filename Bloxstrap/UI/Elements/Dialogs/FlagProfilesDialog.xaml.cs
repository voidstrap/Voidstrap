using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using Hellstrap.Resources;
using Hellstrap.Enums.FlagPresets;
using System.Collections.ObjectModel;
using System.ComponentModel;

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

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);

            if (!Directory.Exists(profilesDirectory))
            {
                Directory.CreateDirectory(profilesDirectory);
            }

            foreach (var profilePath in Directory.GetFiles(profilesDirectory))
            {
                string profileName = Path.GetFileName(profilePath);
                if (!string.IsNullOrEmpty(profileName))
                {
                    LoadProfile.Items.Add(profileName);
                }
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
