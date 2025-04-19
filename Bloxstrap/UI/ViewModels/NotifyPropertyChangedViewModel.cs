using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows.Input;

namespace Voidstrap.UI.ViewModels
{
    public class NotifyPropertyChangedViewModel : INotifyPropertyChanged
    {



        public ICommand OpenModsFolderCommand => new RelayCommand(() => Process.Start("explorer.exe", Paths.Mods));
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
