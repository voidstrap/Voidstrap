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
        private readonly string backgroundImagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Voidstrap",
            "chat_bg.png"
        );

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AIChatPage()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            InitializeComponent();
            LoadSavedBackground();
            DataContext = new AIChatPageViewModel();
        }

        private void AddCustomBackground_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Background Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };

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

        private void LoadSavedBackground()
        {
            if (File.Exists(backgroundImagePath))
            {
                try
                {
                    ApplyBackground(backgroundImagePath);
                }
                catch
                {
                }
            }
        }

        private void ApplyBackground(string path)
        {
            BitmapImage bitmap = new BitmapImage();

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; 
                    bitmap.StreamSource = memoryStream;
                    bitmap.EndInit();
                    bitmap.Freeze(); 
                }
            }

            var imageBrush = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.25 
            };

            ChatBorder.Background = imageBrush;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CommandItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is string command)
            {
                // Assuming DataContext is your ViewModel with UserInput property
                if (DataContext is AIChatPageViewModel vm)
                {
                    vm.UserInput = command;
                }
            }
        }

        private string _userInput;
        public string UserInput
        {
            get => _userInput;
            set
            {
                if (_userInput != value)
                {
                    _userInput = value;
                    OnPropertyChanged(nameof(UserInput));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }



        private void CopyLatestButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AIChatPageViewModel vm)
            {
                var latestMessage = vm.ChatMessages?.LastOrDefault();
                if (!string.IsNullOrEmpty(latestMessage))
                {
                    Clipboard.SetText(latestMessage);
                }
                else
                {
                    return;
                }
            }
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
