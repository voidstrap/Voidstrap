using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Voidstrap.UI.ViewModels
{
    public class NotifyPropertyChangedViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // Automatically infers property name when not provided
        public void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Example command: Opens Mods folder
        public ICommand OpenModsFolderCommand => new RelayCommand(() =>
        {
            Process.Start("explorer.exe", Paths.Mods);
        });
    }
}
