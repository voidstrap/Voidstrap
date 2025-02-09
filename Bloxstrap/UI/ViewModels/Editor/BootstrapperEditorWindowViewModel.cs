using Hellstrap.UI.Elements.Bootstrapper;  // Ensure this is only included once
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
        private async void Preview()  // Mark as async if you perform async actions
        {
            try
            {
                var dialog = new CustomDialog();
                dialog.ApplyCustomTheme(Name, Code);

                // Close previous dialog if it exists
                ClosePreviousDialog();

                // Set message and show the preview dialog
                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;
                dialog.CancelEnabled = true;
                dialog.ShowBootstrapper();
            }
            catch (Exception)
            {
                // Show a confirmation message box to the user
                Frontend.ShowMessageBox(
                    "No Current Theme Added To Preview!",
                    MessageBoxImage.Warning,
                    MessageBoxButton.OK
                );

                // You could log the exception if needed, like:
                // App.Logger.WriteException("Preview method", ex);
            }
        }

        // Save the custom theme to a file
        private void Save()
        {
            string themeDirectory = Path.Combine(Paths.CustomThemes, Name);
            string themeFilePath = Path.Combine(themeDirectory, "Theme.xml");

            try
            {
                EnsureDirectoryExists(themeDirectory);

                // Save the code to the file
                File.WriteAllText(themeFilePath, Code);
            }
            catch (IOException ex)
            {
                HandleError("BootstrapperEditorWindowViewModel::Save", "Error while writing the theme file", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                HandleError("BootstrapperEditorWindowViewModel::Save", "Insufficient permissions to save the theme", ex);
            }
            catch (Exception ex)
            {
                HandleError("BootstrapperEditorWindowViewModel::Save", "Failed to save custom theme", ex);
            }
        }

        // Helper method to close the previous dialog
        private void ClosePreviousDialog()
        {
            _dialog?.CloseBootstrapper();
            _dialog = null;
        }

        // Helper method to ensure the directory exists
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        // Centralized error handling method
        private void HandleError(string logIdentifier, string message, Exception ex)
        {
            // Log error
            App.Logger.WriteLine(logIdentifier, message);
            App.Logger.WriteException(logIdentifier, ex);

            // Show error message to the user
            Frontend.ShowMessageBox($"{message}: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
        }
    }
}
