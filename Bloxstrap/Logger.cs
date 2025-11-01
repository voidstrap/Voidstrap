using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Voidstrap
{
    public class Logger : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private FileStream? _fileStream;

        private const int MaxHistoryEntries = 150;

        public List<string> History { get; } = new();
        public bool Initialized { get; private set; }
        public bool NoWriteMode { get; private set; }
        public string? FileLocation { get; private set; }

        public string AsDocument => string.Join('\n', History);

        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Logger is already initialized");
                return;
            }

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            Directory.CreateDirectory(directory);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            string filename = $"{App.ProjectName}_{timestamp}.log";
            string location = Path.Combine(directory, filename);

            FileLocation = location;

            try
            {
                _fileStream = new FileStream(location, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
                Initialized = true;

                // Flush existing history to the file
                if (History.Count > 0)
                    _ = WriteToLogAsync(string.Join("\r\n", History));

                WriteLine(LOG_IDENT, $"Logger initialized at {location}");
                CleanupOldLogs(directory);
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
            }
            catch (IOException ex)
            {
                WriteLine(LOG_IDENT, $"Failed to initialize due to IO exception: {ex.Message}");
            }
        }

        private void CleanupOldLogs(string directory)
        {
            if (!Paths.Initialized || !Directory.Exists(directory)) return;

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
                    WriteLine("Logger::Cleanup", $"Failed to delete log '{log.Name}'");
                    WriteException("Logger::Cleanup", ex);
                }
            }
        }

        private void WriteLine(string message)
        {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string sanitizedMessage = message.Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);
            string output = $"{timestamp} {sanitizedMessage}";

            Debug.WriteLine(output);
            History.Add(output);

            if (History.Count > MaxHistoryEntries)
                History.RemoveAt(0);

            _ = WriteToLogAsync(output);
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            string hresult = $"0x{ex.HResult:X8}";
            WriteLine($"[{identifier}] ({hresult}) {ex}");
        }

        private async Task WriteToLogAsync(string message)
        {
            if (!Initialized || _fileStream == null) return;

            byte[] buffer = Encoding.UTF8.GetBytes(message + "\r\n");

            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);
                await _fileStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await _fileStream.FlushAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Stream might be disposed during shutdown
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _fileStream?.Dispose();
            _semaphore.Dispose();
        }
    }
}
