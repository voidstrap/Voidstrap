using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Voidstrap.Models
{
    public class NvidiaProfileSetting
    {
        public string Name { get; set; } = "";
        public string SettingId { get; set; } = "";
        public string Value { get; set; } = "";
        public string ValueType { get; set; } = "Dword";
    }

    public class NvidiaEditorEntry : INotifyPropertyChanged
    {
        private string _name = "";
        private string _settingId = "";
        private string _value = "";
        private string _valueType = "Dword";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string SettingId
        {
            get => _settingId;
            set { _settingId = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public string ValueType
        {
            get => _valueType;
            set { _valueType = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
