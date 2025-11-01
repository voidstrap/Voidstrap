using System.ComponentModel;

namespace Voidstrap.Models
{
    public class FastFlag : INotifyPropertyChanged
    {
        private bool _enabled;
        private string _preset = string.Empty;
        private string _name = string.Empty;
        private string _value = string.Empty;
        private bool _index;

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        public string Preset
        {
            get => _preset;
            set { _preset = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
