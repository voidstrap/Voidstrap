using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Voidstrap;
using Voidstrap.AppData;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.ViewModels
{
    public sealed class BackupItem
    {
        public string FileName { get; init; } = "";
        public string FullPath { get; init; } = "";
        public DateTime CreatedUtc { get; init; }
        public string Display => $"{FileName} — {CreatedUtc.ToLocalTime():g}";
    }

    public class AccountBackupsViewModel : INotifyPropertyChanged
    {
        private readonly string _robloxCookiePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Roblox\\LocalStorage\\RobloxCookies.dat");
        private readonly string _backupFolder =
            Path.Combine(Paths.Base, "ProfileBackupsAcc");

        private CancellationTokenSource? _cts;

        public ObservableCollection<BackupItem> Backups { get; } = new();

        private BackupItem? _selected;
        public BackupItem? Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    OnPropertyChanged(nameof(Selected));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _status = "Ready";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        private string _newBackupLabel = "";
        public string NewBackupLabel
        {
            get => _newBackupLabel;
            set { _newBackupLabel = value; OnPropertyChanged(nameof(NewBackupLabel)); }
        }

        public bool CookieDetected => File.Exists(_robloxCookiePath);
        public string CookiePath => _robloxCookiePath;
        public string BackupFolder => _backupFolder;
        public ICommand RefreshCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand DeleteBackupCommand { get; }
        public ICommand OpenBackupsFolderCommand { get; }
        public ICommand LogoutCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public AccountBackupsViewModel()
        {
            Directory.CreateDirectory(_backupFolder);

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreAsync(), _ => Selected is not null);
            DeleteBackupCommand = new RelayCommand(_ => DeleteSelected(), _ => Selected is not null);
            OpenBackupsFolderCommand = new RelayCommand(_ => OpenBackupsFolder());
            LogoutCommand = new RelayCommand(_ => Logout());
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Status = "Scanning backups...";
                Backups.Clear();

                await Task.Run(() =>
                {
                    var dir = new DirectoryInfo(_backupFolder);
                    if (!dir.Exists) return;

                    var items = dir.EnumerateFiles("*.dat", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => f.CreationTimeUtc)
                        .Select(f => new BackupItem
                        {
                            FileName = f.Name,
                            FullPath = f.FullName,
                            CreatedUtc = f.CreationTimeUtc
                        })
                        .ToList();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in items)
                            Backups.Add(item);
                    });
                }, token);

                Status = $"Found {Backups.Count} backup(s).";
                OnPropertyChanged(nameof(CookieDetected));
                OnPropertyChanged(nameof(CookiePath));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                if (!File.Exists(_robloxCookiePath))
                {
                    Frontend.ShowMessageBox("RobloxCookies.dat not found.\nLog into Roblox at least once.");
                    return;
                }

                EnsureRobloxClosed();
                Status = "Creating backup...";
                string safeLabel = Sanitize(_newBackupLabel);
                string fileName = string.IsNullOrWhiteSpace(safeLabel)
                    ? $"cookie_{DateTime.Now:yyyyMMdd_HHmmss_fff}.dat"
                    : $"cookie_{safeLabel}.dat";

                string destination = Path.Combine(_backupFolder, fileName);
                await CopyWithRetryAsync(_robloxCookiePath, destination);

                Status = $"Backup created: {fileName}";
                _newBackupLabel = "";
                OnPropertyChanged(nameof(NewBackupLabel));
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Status = $"Backup failed: {ex.Message}";
                Frontend.ShowMessageBox(ex.Message);
            }
        }

        private async Task RestoreAsync()
        {
            if (Selected is null)
                return;

            try
            {
                EnsureRobloxClosed();

                Status = "Switching account...";
                string safety = Path.Combine(_backupFolder,
                    $"_auto_before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.dat");

                if (File.Exists(_robloxCookiePath))
                    await CopyWithRetryAsync(_robloxCookiePath, safety);

                await CopyWithRetryAsync(Selected.FullPath, _robloxCookiePath, overwrite: true);

                Status = $"Switched to: {Selected.FileName}";
                Frontend.ShowMessageBox($"Switched to backup:\n{Selected.FileName}");
            }
            catch (Exception ex)
            {
                Status = $"Restore failed: {ex.Message}";
                Frontend.ShowMessageBox(ex.Message);
            }
        }

        private void DeleteSelected()
        {
            if (Selected is null)
                return;

            try
            {
                File.Delete(Selected.FullPath);
                Backups.Remove(Selected);
                Selected = null;
                Status = "Backup deleted.";
            }
            catch (Exception ex)
            {
                Status = $"Delete failed: {ex.Message}";
            }
        }
        private void OpenBackupsFolder()
        {
            try
            {
                if (!Directory.Exists(_backupFolder))
                    Directory.CreateDirectory(_backupFolder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = _backupFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Status = $"Failed to open folder: {ex.Message}";
            }
        }
        private void Logout()
        {
            try
            {
                if (File.Exists(CookiePath))
                {
                    File.Delete(CookiePath);
                    Status = "Logged out — Roblox cookie deleted.";
                    OnPropertyChanged(nameof(CookieDetected));
                }
                else
                {
                    Status = "No Roblox cookie found to remove.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Logout failed: {ex.Message}";
            }
        }
        private static async Task CopyWithRetryAsync(string src, string dest, bool overwrite = false, int retries = 5)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    using var s = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var d = new FileStream(dest, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await s.CopyToAsync(d);
                    return;
                }
                catch (IOException) when (i < retries - 1)
                {
                    await Task.Delay(150);
                }
            }
        }

        private static void EnsureRobloxClosed()
        {
            string[] procs = { "RobloxPlayerBeta", "RobloxStudioBeta", "RobloxPlayer", "Roblox" };
            foreach (var name in procs)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (!p.HasExited)
                        throw new IOException("Close all Roblox instances before switching accounts.");
                }
            }
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var clean = Regex.Replace(input.Trim(), @"[^A-Za-z0-9 _\-]", "");
            return Regex.Replace(clean, @"\s+", "_");
        }
    }
}
