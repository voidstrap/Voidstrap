using System.Collections.ObjectModel;
using System.ComponentModel;
using static Voidstrap.UI.Elements.Settings.Pages.FastFlagEditorPage;

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

        public ObservableCollection<string> Tags => FastFlagTagHelper.GetTags(Name);
        private const int MaxVisibleTags = 3;

        public ObservableCollection<string> VisibleTags
        {
            get
            {
                var visible = new ObservableCollection<string>();
                if (Tags.Count <= MaxVisibleTags)
                {
                    foreach (var t in Tags) visible.Add(t);
                }
                else
                {
                    for (int i = 0; i < MaxVisibleTags; i++) visible.Add(Tags[i]);
                    visible.Add($"+{Tags.Count - MaxVisibleTags}");
                }
                return visible;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
