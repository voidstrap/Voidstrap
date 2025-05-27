using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class AIChatPage : Page
    {


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AIChatPage()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            DataContext = new AIChatPageViewModel();
        }

        private void AddCustomBackground_Click(object sender, RoutedEventArgs e)
        {

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backgroundImagePath));

                    File.Copy(openFileDialog.FileName, backgroundImagePath, true);

                    ApplyBackground(backgroundImagePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to apply background: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }


        private void RemoveBackground_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(backgroundImagePath))
                {
                    File.Delete(backgroundImagePath);
                }
                ChatBorder.Background = new SolidColorBrush(Color.FromArgb(0x59, 0x00, 0x00, 0x00));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to remove background: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
