namespace Hellstrap
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class Logger : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private FileStream? _fileStream;
        private readonly ConcurrentQueue<string> _history = new();

        public bool Initialized { get; private set; }
        public bool NoWriteMode { get; private set; }
        public string? FileLocation { get; private set; }
        public string AsDocument => string.Join('\n', _history);

        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Logger is already initialized.");
                return;
            }

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            string filename = $"{App.ProjectName}_{timestamp}.log";
            string location = Path.Combine(directory, filename);

            try
            {
                Directory.CreateDirectory(directory);
                _fileStream = new FileStream(location, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                FileLocation = location;
                Initialized = true;
                WriteLine(LOG_IDENT, $"Logger initialized at {location}");

                FlushHistoryToLog();
                CleanupOldLogs();
            }
            catch (IOException)
            {
                WriteLine(LOG_IDENT, "Log file already exists. Initialization aborted.");
            }
            catch (UnauthorizedAccessException)
            {
                HandleUnauthorizedAccess(directory);
            }
            catch (Exception ex)
            {
                WriteException(LOG_IDENT, ex);
            }
        }

        private void FlushHistoryToLog()
        {
            if (!Initialized || _history.IsEmpty) return;

            var sb = new StringBuilder();
            while (_history.TryDequeue(out string? log))
            {
                sb.AppendLine(log);
            }
            WriteToLog(sb.ToString());
        }

        private void CleanupOldLogs()
        {
            if (!Paths.Initialized || !Directory.Exists(Paths.Logs)) return;

            foreach (FileInfo log in new DirectoryInfo(Paths.Logs).GetFiles())
            {
                if (log.LastWriteTimeUtc.AddDays(7) > DateTime.UtcNow) continue;

                try
                {
                    log.Delete();
                    WriteLine("Logger::Cleanup", $"Deleted old log file: {log.Name}");
                }
                catch (Exception ex)
                {
                    WriteException("Logger::Cleanup", ex);
                }
            }
        }

        private void HandleUnauthorizedAccess(string directory)
        {
            const string LOG_IDENT = "Logger::Initialize";
            if (NoWriteMode) return;

            WriteLine(LOG_IDENT, $"No write access to {directory}. Switching to NoWriteMode.");
            Frontend.ShowMessageBox(string.Format(Strings.Logger_NoWriteMode, directory),
                                    System.Windows.MessageBoxImage.Warning,
                                    System.Windows.MessageBoxButton.OK);

            NoWriteMode = true;
        }

        private void WriteLine(string message)
        {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string formattedMessage = $"{timestamp} {message}".Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);

            Debug.WriteLine(formattedMessage);
            _history.Enqueue(formattedMessage);
            WriteToLog(formattedMessage);
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            string hresult = $"0x{ex.HResult:X8}";
            WriteLine(identifier, $"Exception: {hresult} - {ex.Message}\n{ex.StackTrace}");
        }

        private async void WriteToLog(string message)
        {
            if (!Initialized || _fileStream is null) return;

            byte[] data = Encoding.UTF8.GetBytes(message + Environment.NewLine);
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await _fileStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                await _fileStream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger::WriteToLog] Error writing to log: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
            _fileStream?.Dispose();
        }
    }
}
