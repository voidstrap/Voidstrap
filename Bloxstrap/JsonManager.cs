using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace Voidstrap
{
    public class JsonManager<T> where T : class, new()
    {
        public T OriginalProp { get; set; } = new();
        public T Prop { get; set; } = new();
        public virtual string ClassName => typeof(T).Name;
        public string? LastFileHash { get; private set; }
        public virtual string BackupsLocation => Path.Combine(Paths.Base, "Backup.json");
        public virtual string FileLocation => Path.Combine(Paths.Base, $"{ClassName}.json");
        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public virtual void Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";
            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (!File.Exists(FileLocation))
                {
                    App.Logger.WriteLine(LOG_IDENT, "File does not exist, saving defaults.");
                    Save();
                    return;
                }

                string json;
                using (var stream = new FileStream(FileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    json = reader.ReadToEnd();

                T? settings = JsonSerializer.Deserialize<T>(json);
                if (settings is null)
                    throw new InvalidOperationException("Deserialization returned null.");

                Prop = settings;

                LastFileHash = SafeGetFileHash(FileLocation);
                App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                {
                    Frontend.ShowMessageBox($"Failed to load settings:\n\n{ex.Message}", MessageBoxImage.Warning);

                    try
                    {
                        string backupPath = FileLocation + ".bak";
                        File.Copy(FileLocation, backupPath, true);
                        App.Logger.WriteLine(LOG_IDENT, $"Created backup file: {backupPath}");
                    }
                    catch (Exception copyEx)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to create backup file: {FileLocation}.bak");
                        App.Logger.WriteException(LOG_IDENT, copyEx);
                    }
                }

                Save();
            }
        }

        public virtual void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            const int maxRetries = 5;
            const int delayMs = 100;
            int attempts = 0;

            while (true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(FileLocation, json);

                    LastFileHash = SafeGetFileHash(FileLocation);
                    App.Logger.WriteLine(LOG_IDENT, "Save complete!");
                    break;
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32 && attempts < maxRetries)
                {
                    attempts++;
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException ex) when (attempts < maxRetries)
                {
                    attempts++;
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                    Frontend.ShowMessageBox(errorMessage, MessageBoxImage.Warning);
                    break;
                }
            }
        }

        private static string? SafeGetFileHash(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    return MD5Hash.FromFile(path);
            }
            catch
            {
                return null;
            }
        }

        public bool HasFileOnDiskChanged()
        {
            try
            {
                string? currentHash = SafeGetFileHash(FileLocation);
                return LastFileHash != currentHash;
            }
            catch
            {
                return true;
            }
        }

        public void SaveBackup(string name)
        {
            const string LOGGER_STRING = "SaveBackup::Backups";
            string baseDir = Paths.SavedBackups;

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                Directory.CreateDirectory(baseDir);

                string filePath = Path.Combine(baseDir, name);
                string json = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                App.Logger.WriteLine(LOGGER_STRING, $"Backup '{name}' saved successfully.");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save backup:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        public void LoadBackup(string? name, bool? clearFlags)
        {
            const string LOGGER_STRING = "LoadBackup::Backups";
            string baseDir = Paths.SavedBackups;

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                string filePath = Path.Combine(baseDir, name);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Backup file '{name}' not found.");

                string json;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    json = reader.ReadToEnd();

                T? settings = JsonSerializer.Deserialize<T>(json);
                if (settings is null)
                    throw new InvalidOperationException("Deserialization returned null.");

                if (clearFlags == true)
                {
                    Prop = settings;
                }
                else if (settings is IDictionary<string, object> settingsDict && Prop is IDictionary<string, object> propDict)
                {
                    foreach (var kvp in settingsDict)
                    {
                        if (kvp.Value != null)
                            propDict[kvp.Key] = kvp.Value;
                    }
                }

                App.Logger.WriteLine(LOGGER_STRING, $"Backup '{name}' loaded successfully.");
                App.FastFlags.Save();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOGGER_STRING, ex);
                Frontend.ShowMessageBox($"Failed to load backup:\n{ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}