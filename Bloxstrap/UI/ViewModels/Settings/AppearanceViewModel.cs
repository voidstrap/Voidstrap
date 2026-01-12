using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Voidstrap;
using Voidstrap.UI.Elements.Bootstrapper;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.Elements.Editor;
using Voidstrap.UI.Elements.Settings;
using Voidstrap.UI.ViewModels;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Page _page;

        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public ICommand BrowseCustomIconLocationCommand => new RelayCommand(BrowseCustomIconLocation);
        public ICommand AddCustomThemeCommand => new RelayCommand(AddCustomTheme);
        public ICommand DeleteCustomThemeCommand => new RelayCommand(DeleteCustomTheme);
        public ICommand RenameCustomThemeCommand => new RelayCommand(RenameCustomTheme);
        public ICommand EditCustomThemeCommand => new RelayCommand(EditCustomTheme);
        public ICommand ExportCustomThemeCommand => new RelayCommand(ExportCustomTheme);
        public ICommand ImportBackgroundCommand { get; }
        public ICommand RemoveBackgroundCommand { get; }

        public ICommand ImportStartupAudioCommand { get; }
        public ICommand RemoveStartupAudioCommand { get; }

        private readonly MediaPlayer _audioPlayer = new();
        private const string FileName = "BackgroundSettings.json";
        private static readonly string FilePath = Path.Combine(Paths.Base, FileName);

        private BackgroundSettings _settings;

        public static class AudioEvents
        {
            public static event Action<string?>? StartupAudioChanged;

            public static void RaiseStartupAudioChanged(string? path)
            {
                StartupAudioChanged?.Invoke(path);
            }
        }

        public AppearanceViewModel()
        {
            ImportBackgroundCommand = new RelayCommand(ImportBackground);
            RemoveBackgroundCommand = new RelayCommand(RemoveBackground);
            ImportStartupAudioCommand = new RelayCommand(ImportStartupAudio);
            RemoveStartupAudioCommand = new RelayCommand(RemoveStartupAudio);
            _settings = LoadSettings();

            ImportBackgroundCommand2 = new RelayCommand<object>(ImportFile);
            RemoveBackgroundCommand2 = new RelayCommand<object>(RemoveFile);

            foreach (var entry in BootstrapperIconEx.Selections)
                Icons.Add(new BootstrapperIconEntry { IconType = entry });

            PopulateCustomThemes();
        }

        public bool Snowww
        {
            get => App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw;
            set => App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw = value;
        }

        public bool GRADmentFR
        {
            get => App.Settings.Prop.GRADmentFR;
            set => App.Settings.Prop.GRADmentFR = value;
        }

        #region Properties

        public string? BackgroundFilePath
        {
            get => _settings.BackgroundFilePath;
            set
            {
                if (_settings.BackgroundFilePath != value)
                {
                    _settings.BackgroundFilePath = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public double GradientOpacity
        {
            get => _settings.GradientOpacity;
            set
            {
                if (_settings.GradientOpacity != value)
                {
                    _settings.GradientOpacity = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand ImportBackgroundCommand2 { get; }
        public ICommand RemoveBackgroundCommand2 { get; }

        private void ImportFile(object? _)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image/Video Files|*.png;*.jpg;*.jpeg;*.gif;*.mp4;*.mov",
                Title = "Select Background File"
            };

            if (dialog.ShowDialog() == true)
            {
                BackgroundFilePath = dialog.FileName;
            }
        }

        private void RemoveFile(object? _)
        {
            BackgroundFilePath = null;
        }

        #endregion

        #region JSON Load/Save

        private static BackgroundSettings LoadSettings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<BackgroundSettings>(json) ?? new BackgroundSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load background settings: {ex.Message}");
            }
            return new BackgroundSettings();
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save background settings: {ex.Message}");
            }
        }

        #endregion

        public class BackgroundSettings
        {
            public string? BackgroundFilePath { get; set; } = null;
            public double GradientOpacity { get; set; } = 1;
        }

        #region Properties

        public IEnumerable<Theme> Themes { get; } = Enum.GetValues(typeof(Theme)).Cast<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme2;
            set
            {
                App.Settings.Prop.Theme2 = value;
                ((MainWindow)Window.GetWindow(_page)!).ApplyTheme();
            }
        }

        public static List<string> Languages => Locale.GetLanguages();

        public string SelectedLanguage
        {
            get => Locale.SupportedLocales[App.Settings.Prop.Locale];
            set => App.Settings.Prop.Locale = Locale.GetIdentifierFromName(value);
        }

        public IEnumerable<BootstrapperStyle> Dialogs { get; } = BootstrapperStyleEx.Selections;

        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set
            {
                App.Settings.Prop.BootstrapperStyle = value;
                OnPropertyChanged(nameof(CustomThemesExpanded));
            }
        }

        public bool CustomThemesExpanded => App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.CustomDialog;

        public ObservableCollection<BootstrapperIconEntry> Icons { get; set; } = new();

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
                if (string.IsNullOrEmpty(value))
                {
                    if (App.Settings.Prop.BootstrapperIcon == BootstrapperIcon.IconCustom)
                        App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconVoidstrap;
                }
                else
                {
                    App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconCustom;
                }

                App.Settings.Prop.BootstrapperIconCustomLocation = value;

                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Icons));
            }
        }

        public string? SelectedCustomTheme
        {
            get => App.Settings.Prop.SelectedCustomTheme;
            set
            {
                App.Settings.Prop.SelectedCustomTheme = value;
                OnPropertyChanged(nameof(IsCustomThemeSelected));
            }
        }

        public string SelectedCustomThemeName { get; set; } = "";

        public int SelectedCustomThemeIndex { get; set; }

        public ObservableCollection<string> CustomThemes { get; set; } = new();

        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;

        #endregion

        #region Commands

        private void PreviewBootstrapper()
        {
            IBootstrapperDialog dialog = App.Settings.Prop.BootstrapperStyle.GetNew();

            dialog.Message = App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.ByfronDialog
                ? Strings.Bootstrapper_StylePreview_ImageCancel
                : Strings.Bootstrapper_StylePreview_TextCancel;

            dialog.CancelEnabled = true;
            AudioPlayerHelper.PlayStartupAudio();
            if (dialog is Window window)
            {
                window.Closed += (s, e) =>
                {
                    Voidstrap.UI.Elements.Bootstrapper.AudioPlayerHelper.StopAudio();
                };
            }

            dialog.ShowBootstrapper();
        }

        private void BrowseCustomIconLocation()
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_IconFiles}|*.ico"
            };

            if (dialog.ShowDialog() == true)
            {
                CustomIconLocation = dialog.FileName;
            }
        }

        private void AddCustomTheme()
        {
            var dialog = new AddCustomThemeDialog();
            dialog.ShowDialog();

            if (dialog.Created)
            {
                CustomThemes.Add(dialog.ThemeName);
                SelectedCustomThemeIndex = CustomThemes.Count - 1;

                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(IsCustomThemeSelected));
                if (dialog.OpenEditor)
                    EditCustomTheme();
            }
        }

        #region Startup Audio Handling
        private void ImportStartupAudio()
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select a Startup Sound",
                Filter = "Audio Files|*.mp3;*.wav;*.ogg;*.flac;*.wma",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            };

            if (openDialog.ShowDialog() != true)
                return;

            string selectedPath = openDialog.FileName;
            if (!File.Exists(selectedPath))
                return;

            try
            {
                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "startup_audio.*"))
                {
                    try { File.Delete(old); } catch { }
                }
                string newFileName = "startup_audio" + Path.GetExtension(selectedPath);
                string newPath = Path.Combine(Paths.Base, newFileName);
                File.Copy(selectedPath, newPath, overwrite: true);
                AudioEvents.RaiseStartupAudioChanged(newPath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::ImportStartupAudio", ex);
            }
        }

        private void RemoveStartupAudio()
        {
            try
            {
                foreach (var old in Directory.GetFiles(Paths.Base, "startup_audio.*"))
                {
                    try { File.Delete(old); } catch { }
                }

                AudioEvents.RaiseStartupAudioChanged(null);
                _audioPlayer.Stop();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::RemoveStartupAudio", ex);
            }
        }

        #endregion

        private void DeleteCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            try
            {
                DeleteCustomThemeStructure(SelectedCustomTheme);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::DeleteCustomTheme", ex);
                Frontend.ShowMessageBox(
                    string.Format(Strings.Menu_Appearance_CustomThemes_DeleteFailed, SelectedCustomTheme, ex.Message),
                    MessageBoxImage.Error
                );
                return;
            }

            CustomThemes.Remove(SelectedCustomTheme);

            if (CustomThemes.Any())
            {
                SelectedCustomThemeIndex = CustomThemes.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            }

            SelectedCustomTheme = null;
        }

        private void RenameCustomTheme()
        {
            if (SelectedCustomTheme is null || SelectedCustomTheme == SelectedCustomThemeName)
                return;

            try
            {
                RenameCustomThemeStructure(SelectedCustomTheme, SelectedCustomThemeName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::RenameCustomTheme", ex);
                Frontend.ShowMessageBox(
                    string.Format(Strings.Menu_Appearance_CustomThemes_RenameFailed, SelectedCustomTheme, ex.Message),
                    MessageBoxImage.Error
                );
                return;
            }

            int idx = CustomThemes.IndexOf(SelectedCustomTheme);
            CustomThemes[idx] = SelectedCustomThemeName;

            SelectedCustomThemeIndex = idx;
            OnPropertyChanged(nameof(SelectedCustomThemeIndex));
        }

        private void EditCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;
            new BootstrapperEditorWindow(SelectedCustomTheme).ShowDialog();
        }

        private void ExportCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            var dialog = new SaveFileDialog
            {
                FileName = $"{SelectedCustomTheme}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip"
            };

            if (dialog.ShowDialog() != true)
                return;

            string themeDir = Path.Combine(Paths.CustomThemes, SelectedCustomTheme);

            using var memStream = new MemoryStream();
            using var zipStream = new ZipOutputStream(memStream) { IsStreamOwner = false };

            foreach (var filePath in Directory.EnumerateFiles(themeDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(themeDir, filePath);
                var entry = new ZipEntry(relativePath) { DateTime = DateTime.Now };
                zipStream.PutNextEntry(entry);

                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(zipStream);
            }

            zipStream.Finish();
            memStream.Position = 0;

            using var outputStream = File.OpenWrite(dialog.FileName);
            memStream.CopyTo(outputStream);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{dialog.FileName}\"",
                UseShellExecute = true
            });
        }

        #endregion

        private void ImportBackground()
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select a Background Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openDialog.ShowDialog() != true)
                return;

            string selectedPath = openDialog.FileName;
            if (!File.Exists(selectedPath))
                return;
            try
            {
                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "bootstrapper_bg.*"))
                {
                    try { File.Delete(old); } catch { }
                }
                string newFileName = "bootstrapper_bg" + Path.GetExtension(selectedPath);
                string newPath = Path.Combine(Paths.Base, newFileName);
                File.Copy(selectedPath, newPath, overwrite: true);
                BackgroundEvents.RaiseBackgroundChanged(newPath);
            }
            catch (Exception)
            {
            }
        }
        #region Custom Theme Helpers

        private void DeleteCustomThemeStructure(string name)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);
            Directory.Delete(dir, true);
        }

        private void RemoveBackground()
        {
            try
            {
                foreach (var old in Directory.GetFiles(Paths.Base, "bootstrapper_bg.*"))
                {
                    try { File.Delete(old); } catch { }
                }
                BackgroundEvents.RaiseBackgroundChanged(null);
            }
            catch (Exception)
            {
            }
        }

        private void RenameCustomThemeStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomThemes, oldName);
            string newDir = Path.Combine(Paths.CustomThemes, newName);
            Directory.Move(oldDir, newDir);
        }

        private void PopulateCustomThemes()
        {
            string? selected = App.Settings.Prop.SelectedCustomTheme;

            Directory.CreateDirectory(Paths.CustomThemes);

            foreach (string directory in Directory.GetDirectories(Paths.CustomThemes))
            {
                if (!File.Exists(Path.Combine(directory, "Theme.xml")))
                    continue;

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

        #endregion
    }
}
