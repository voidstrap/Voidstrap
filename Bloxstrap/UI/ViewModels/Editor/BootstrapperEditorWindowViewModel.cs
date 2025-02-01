using Hellstrap.UI.Elements.Bootstrapper;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Hellstrap.UI.ViewModels.Editor
{
    public class BootstrapperEditorWindowViewModel : NotifyPropertyChangedViewModel
    {
        private CustomDialog? _dialog = null;

        // Commands
        public ICommand PreviewCommand { get; }
        public ICommand SaveCommand { get; }

        // Properties
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = "Editing \"Custom Theme\"";
        public string Code { get; set; } = string.Empty;

        public BootstrapperEditorWindowViewModel()
        {
            // Initialize commands
            PreviewCommand = new RelayCommand(Preview);
            SaveCommand = new RelayCommand(Save);
        }

        // Preview the custom theme
        private void Preview()
        {
            const string LogIdentifier = "BootstrapperEditorWindowViewModel::Preview";

            try
            {
                var dialog = new CustomDialog();
                dialog.ApplyCustomTheme(Name, Code);

                // Close previous dialog if it exists
                _dialog?.CloseBootstrapper();
                _dialog = dialog;

                // Set message and show the preview dialog
                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;
                dialog.CancelEnabled = true;
                dialog.ShowBootstrapper();
            }
            catch (Exception ex)
            {
                // Log error
                App.Logger.WriteLine(LogIdentifier, "Failed to preview custom theme");
                App.Logger.WriteException(LogIdentifier, ex);

            }
        }

        // Save the custom theme to a file
        private void Save()
        {
            const string LogIdentifier = "BootstrapperEditorWindowViewModel::Save";
            string themeDirectory = Path.Combine(Paths.CustomThemes, Name);
            string themeFilePath = Path.Combine(themeDirectory, "Theme.xml");

            try
            {
                // Ensure the directory exists
                if (!Directory.Exists(themeDirectory))
                {
                    Directory.CreateDirectory(themeDirectory);
                }

                // Save the code to the file
                File.WriteAllText(themeFilePath, Code);
            }
            catch (Exception ex)
            {
                // Log error
                App.Logger.WriteLine(LogIdentifier, "Failed to save custom theme");
                App.Logger.WriteException(LogIdentifier, ex);

                // Show error message to the user
                Frontend.ShowMessageBox($"Failed to save theme: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }
    }
}
