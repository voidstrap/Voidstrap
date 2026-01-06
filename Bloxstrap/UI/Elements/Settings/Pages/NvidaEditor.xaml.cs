using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Xml;
using Voidstrap.Integrations;
using Voidstrap.Models;
using Voidstrap.UI.Elements.Dialogs;
using Wpf.Ui.Controls;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class NvidiaFFlagEditorPage : UiPage, INotifyPropertyChanged
    {
        public ObservableCollection<NvidiaEditorEntry> Entries { get; }
            = new ObservableCollection<NvidiaEditorEntry>();

        private static readonly string NipDirectory =
            Path.Combine(Paths.Base, "NipProfiles");

        private static readonly string NipPath =
            Path.Combine(NipDirectory, "Voidstrap.nip");

        private FileSystemWatcher? _watcher;
        private bool _pendingSave;
        private bool _internalWrite;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<string> ValueTypes { get; } = new()
        {
            "Dword",
            "String",
            "Binary",
            "Boolean",
            "Hex"
        };

        private ICollectionView _entriesView;
        private string _searchText = string.Empty;

        public ICollectionView EntriesView
        {
            get => _entriesView;
            private set
            {
                _entriesView = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(EntriesView)));
            }
        }

        public NvidiaFFlagEditorPage()
        {
            InitializeComponent();
            DataContext = this;

            EntriesView = CollectionViewSource.GetDefaultView(Entries);
            EntriesView.Filter = FilterEntries;

            EnsureNipIsValid();
            SetupFileWatcher();
            LoadFromNipSafe();
        }

        private bool FilterEntries(object obj)
        {
            if (obj is not NvidiaEditorEntry entry)
                return false;

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || entry.SettingId.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || entry.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || entry.ValueType.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
                _pendingSave = true;
        }

        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (!_pendingSave)
                return;

            _pendingSave = false;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, SaveEntries);
        }

        private void SaveEntries()
        {
            try
            {
                _internalWrite = true;
                NvidiaProfileManager.SaveToNip(NipPath, Entries.ToList());
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save NIP file:\n{ex.Message}");
            }
            finally
            {
                _internalWrite = false;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb)
                return;

            _searchText = tb.Text.Trim();
            EntriesView.Refresh();
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (DataGridRef.SelectedItems.Count == 0)
            {
                Frontend.ShowMessageBox("No entries selected.");
                return;
            }

            if (Frontend.ShowMessageBox(
                    $"Are you sure you want to delete {DataGridRef.SelectedItems.Count} selected flags?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            var toRemove = DataGridRef.SelectedItems
                .OfType<NvidiaEditorEntry>()
                .ToList();

            foreach (var entry in toRemove)
                Entries.Remove(entry);

            SaveEntries();
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(NipPath))
                {
                    Frontend.ShowMessageBox("NIP file not found.");
                    return;
                }

                string nipContent = File.ReadAllText(NipPath, Encoding.Unicode);
                Clipboard.SetText(nipContent);

                Frontend.ShowMessageBox(
                    "NIP file copied to clipboard.",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to copy NIP file:\n{ex.Message}",
                    MessageBoxImage.Error);
            }
        }

        private void ExportNip_Click(object sender, RoutedEventArgs e)
        {
            if (!Entries.Any())
            {
                Frontend.ShowMessageBox("No NVIDIA settings to export.");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export NVIDIA Profile",
                Filter = "NVIDIA Profile (*.nip)|*.nip",
                FileName = "Voidstrap.nip"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                NvidiaProfileManager.SaveToNip(dialog.FileName, Entries.ToList());

                Frontend.ShowMessageBox(
                    "NVIDIA profile exported successfully.",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to export NIP file:\n\n{ex.Message}",
                    MessageBoxImage.Error);
            }
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (!Entries.Any())
            {
                Frontend.ShowMessageBox("No entries to delete.");
                return;
            }

            var visible = EntriesView.Cast<NvidiaEditorEntry>().ToList();

            if (visible.Count == 0)
                return;

            if (Frontend.ShowMessageBox(
                    "Are you sure you want to delete all flags?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var entry in visible)
                Entries.Remove(entry);

            SaveEntries();
        }

        private void LoadFromNipSafe()
        {
            try
            {
                Entries.Clear();

                var loaded = NvidiaProfileManager
                    .LoadFromNip(NipPath)
                    .DistinctBy(x => $"{x.SettingId}:{x.Name}");

                foreach (var entry in loaded)
                    Entries.Add(entry);

                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(Entries)));
            }
            catch (XmlException)
            {
                RecreateNipFile();
                LoadFromNipSafe();
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddNvidiaFFlagWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
                return;

            foreach (var entry in dialog.ResultEntries)
            {
                if (Entries.Any(x =>
                    x.SettingId == entry.SettingId &&
                    x.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Entries.Add(entry);
            }
            SaveEntries();
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            SaveEntries();
            await NvidiaProfileManager.ApplyNipFile(NipPath);
            LoadFromNipSafe();
        }

        private void SetupFileWatcher()
        {
            Directory.CreateDirectory(NipDirectory);

            _watcher = new FileSystemWatcher(NipDirectory, "Voidstrap.nip")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Changed += (_, __) =>
            {
                if (_internalWrite)
                    return;

                Dispatcher.BeginInvoke(LoadFromNipSafe);
            };

            _watcher.EnableRaisingEvents = true;
        }

        private static void EnsureNipIsValid()
        {
            Directory.CreateDirectory(NipDirectory);

            if (!File.Exists(NipPath))
            {
                RecreateNipFile();
                return;
            }

            try
            {
                using var reader = XmlReader.Create(NipPath);
                while (reader.Read()) { }
            }
            catch
            {
                RecreateNipFile();
            }
        }

        private async void ResetNipFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Entries.Clear();
                RecreateNipFile();
                SaveEntries();
                await NvidiaProfileManager.ApplyNipFile(NipPath);
                LoadFromNipSafe();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Error while resetting NIP file: {ex.Message}");
            }
        }

        private void BackButton(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new NvidiaFastFlagsPage());
        }

        private static void RecreateNipFile()
        {
            Directory.CreateDirectory(NipDirectory);

            File.WriteAllText(
                NipPath,
        @"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfProfile>
  <Profile>
    <ProfileName>Voidstrap</ProfileName>
    <Executeables>
      <string>robloxplayerbeta.exe</string>
    </Executeables>
    <Settings>
    </Settings>
  </Profile>
</ArrayOfProfile>",
                new UnicodeEncoding(false, true)
            );
        }
    }
}
