using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Voidstrap;

namespace Voidstrap
{
    public class Logger : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private FileStream? _filestream;

        public readonly List<string> History = new();
        public bool Initialized { get; private set; } = false;
        public bool NoWriteMode { get; private set; } = false;
        public string? FileLocation { get; private set; }

        public string AsDocument => string.Join('\n', History);

        private const int MaxHistoryEntries = 150;

        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            string filename = $"{App.ProjectName}_{timestamp}.log";
            string location = Path.Combine(directory, filename);

            WriteLine(LOG_IDENT, $"Initializing at {location}");

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because logger is already initialized");
                return;
            }

            Directory.CreateDirectory(directory);

            try
            {
                _filestream = new FileStream(location, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            }
            catch (IOException)
            {
                WriteLine(LOG_IDENT, "Failed to initialize due to IO exception");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (NoWriteMode) return;

                WriteLine(LOG_IDENT, $"No write access to {directory}");

                Frontend.ShowMessageBox(
                    string.Format(Strings.Logger_NoWriteMode, directory),
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxButton.OK
                );

                NoWriteMode = true;
                return;
            }

            Initialized = true;
            FileLocation = location;

            if (History.Count > 0)
                _ = WriteToLogAsync(string.Join("\r\n", History));

            WriteLine(LOG_IDENT, "Finished initializing!");

            CleanupOldLogs(directory);
        }

        private void CleanupOldLogs(string directory)
        {
            if (!Paths.Initialized || !Directory.Exists(directory))
                return;

            foreach (FileInfo log in new DirectoryInfo(directory).GetFiles())
            {
                if (log.LastWriteTimeUtc.AddDays(7) > DateTime.UtcNow)
                    continue;

                try
                {
                    log.Delete();
                    WriteLine("Logger::Cleanup", $"Deleted old log file '{log.Name}'");
                }
                catch (Exception ex)
                {
                    WriteLine("Logger::Cleanup", "Failed to delete log!");
                    WriteException("Logger::Cleanup", ex);
                }
            }
        }

        private void WriteLine(string message)
        {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string outCon = $"{timestamp} {message}";
            string outLog = outCon.Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);

            Debug.WriteLine(outCon);
            _ = WriteToLogAsync(outLog);

            History.Add(outLog);
            if (History.Count > MaxHistoryEntries)
                History.RemoveAt(0);
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            string hresult = "0x" + ex.HResult.ToString("X8");
            string formatted = $"[{identifier}] ({hresult}) {ex}";

            WriteLine(formatted);
        }

        private async Task WriteToLogAsync(string message)
        {
            if (!Initialized || _filestream == null)
                return;

            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);

                byte[] buffer = Encoding.UTF8.GetBytes($"{message}\r\n");
                await _filestream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await _filestream.FlushAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Stream might be closed on shutdown
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _filestream?.Dispose();
            _semaphore?.Dispose();
        }
    }
}
