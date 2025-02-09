using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Hellstrap.UI.Elements.Settings;
using Hellstrap.UI.Elements.Editor;

namespace Hellstrap.UI.ViewModels.Settings
{
    public class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Page _page;
        private string _downloadingStatus = "";

        public AppearanceViewModel(Page page)
        {
            _page = page;
            Icons = new ObservableCollection<BootstrapperIconEntry>(BootstrapperIconEx.Selections.Select(entry => new BootstrapperIconEntry { IconType = entry }));
            PopulateCustomThemes();
        }

        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public ICommand BrowseCustomIconLocationCommand => new RelayCommand(BrowseCustomIconLocation);
        public ICommand AddCustomThemeCommand => new RelayCommand(AddCustomTheme);
        public ICommand DeleteCustomThemeCommand => new RelayCommand(DeleteCustomTheme);
        public ICommand RenameCustomThemeCommand => new RelayCommand(RenameCustomTheme);
        public ICommand EditCustomThemeCommand => new RelayCommand(EditCustomTheme);

        public IEnumerable<Theme> Themes => Enum.GetValues(typeof(Theme)).Cast<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme;
            set
            {
                App.Settings.Prop.Theme = value;
                ((MainWindow)Window.GetWindow(_page)!)?.ApplyTheme();
            }
        }

        public static List<string> Languages => Locale.GetLanguages();

        public string SelectedLanguage
        {
            get => Locale.SupportedLocales[App.Settings.Prop.Locale];
            set => App.Settings.Prop.Locale = Locale.GetIdentifierFromName(value);
        }

        public string DownloadingStatus
        {
            get => string.Format(Strings.Bootstrapper_Status_Downloading + " {0} - {1}MB / {2}MB", _downloadingStatus);
            set => _downloadingStatus = value;
        }

        public IEnumerable<BootstrapperStyle> Dialogs => BootstrapperStyleEx.Selections;

        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set => App.Settings.Prop.BootstrapperStyle = value;
        }

        public ObservableCollection<BootstrapperIconEntry> Icons { get; }

        public BootstrapperIcon Icon
        {
            get => App.Settings.Prop.BootstrapperIcon;
            set => App.Settings.Prop.BootstrapperIcon = value;
        }

        public string Title
        {
            get => App.Settings.Prop.BootstrapperTitle;
            set => App.Settings.Prop.BootstrapperTitle = value;
        }

        public string CustomIconLocation
        {
            get => App.Settings.Prop.BootstrapperIconCustomLocation;
            set
            {
                App.Settings.Prop.BootstrapperIcon = string.IsNullOrEmpty(value) ? BootstrapperIcon.IconHellstrap : BootstrapperIcon.IconCustom;
                App.Settings.Prop.BootstrapperIconCustomLocation = value;
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Icons));
            }
        }

        public ObservableCollection<string> CustomThemes { get; } = new();
        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;

        public string? SelectedCustomTheme
        {
            get => App.Settings.Prop.SelectedCustomTheme;
            set => App.Settings.Prop.SelectedCustomTheme = value;
        }

        public string SelectedCustomThemeName { get; set; } = "";
        public int SelectedCustomThemeIndex { get; set; }


        private void PopulateCustomThemes()
        {
            string? selected = App.Settings.Prop.SelectedCustomTheme;

            Directory.CreateDirectory(Paths.CustomThemes);

            foreach (string directory in Directory.GetDirectories(Paths.CustomThemes))
            {
                if (!File.Exists(Path.Combine(directory, "Theme.xml")))
                    continue; // missing the main theme file, ignore

                string name = Path.GetFileName(directory);
                CustomThemes.Add(name);
            }

            if (selected != null)
            {
                int idx = CustomThemes.IndexOf(selected);

                if (idx != -1)
                {
                    SelectedCustomThemeIndex = idx;
                    OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                }
                else
                {
                    SelectedCustomTheme = null;
                }
            }
        }

        private void PreviewBootstrapper()
        {
            IBootstrapperDialog dialog = App.Settings.Prop.BootstrapperStyle.GetNew();
            dialog.CancelEnabled = true;
            dialog.ShowBootstrapper();
        }

        private void BrowseCustomIconLocation()
        {
            var dialog = new OpenFileDialog { Filter = $"{Strings.Menu_IconFiles}|*.ico" };
            if (dialog.ShowDialog() == true)
            {
                CustomIconLocation = dialog.FileName;
                OnPropertyChanged(nameof(CustomIconLocation));
            }
        }

        private string GenerateUniqueThemeName()
        {
            int count = Directory.GetDirectories(Paths.CustomThemes).Count();
            string name = $"Theme {count + 1}";

            while (Directory.Exists(Path.Combine(Paths.CustomThemes, name)))
                name += $" {Random.Shared.Next(1, 100000)}";

            return name;
        }

        private void CreateCustomThemeStructure(string name)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Theme.xml"), Encoding.UTF8.GetString(Resource.Get("CustomBootstrapperTemplate.xml").Result));
        }

        private void AddCustomTheme()
        {
            string name = GenerateUniqueThemeName();
            try
            {
                CreateCustomThemeStructure(name);
                CustomThemes.Add(name);
                SelectedCustomThemeIndex = CustomThemes.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(IsCustomThemeSelected));
            }
            catch (Exception ex)
            {
                HandleException("AddCustomTheme", ex);
            }
        }

        private void DeleteCustomTheme()
        {
            if (SelectedCustomTheme is null) return;
            try
            {
                Directory.Delete(Path.Combine(Paths.CustomThemes, SelectedCustomTheme), true);
                CustomThemes.Remove(SelectedCustomTheme);
                SelectedCustomThemeIndex = CustomThemes.Any() ? CustomThemes.Count - 1 : -1;
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(IsCustomThemeSelected));
            }
            catch (Exception ex)
            {
                HandleException("DeleteCustomTheme", ex);
            }
        }

        private void RenameCustomTheme()
        {
            if (SelectedCustomTheme is null || SelectedCustomTheme == SelectedCustomThemeName) return;
            try
            {
                Directory.Move(Path.Combine(Paths.CustomThemes, SelectedCustomTheme), Path.Combine(Paths.CustomThemes, SelectedCustomThemeName));
                CustomThemes[CustomThemes.IndexOf(SelectedCustomTheme)] = SelectedCustomThemeName;
                SelectedCustomThemeIndex = CustomThemes.IndexOf(SelectedCustomThemeName);
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            }
            catch (Exception ex)
            {
                HandleException("RenameCustomTheme", ex);
            }
        }

        private void EditCustomTheme()
        {
            if (SelectedCustomTheme is null) return;
            new BootstrapperEditorWindow(SelectedCustomTheme).ShowDialog();
        }

        private void HandleException(string methodName, Exception ex)
        {
            App.Logger.WriteException($"AppearanceViewModel::{methodName}", ex);
            Frontend.ShowMessageBox($"Error in {methodName}: {ex.Message}", MessageBoxImage.Error);
        }
    }
}
