using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Diagnostics;

namespace Voidstrap
{
    public class Logger : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _logTask;

        private FileStream? _filestream;

        public readonly List<string> History = new();
        public bool Initialized { get; private set; } = false;
        public bool NoWriteMode { get; private set; } = false;
        public string? FileLocation { get; private set; }

        private bool IsLoggingEnabled => Initialized && !NoWriteMode;

        // Example throttling
        private DateTime _lastLogTime = DateTime.MinValue;
        private readonly TimeSpan _logCooldown = TimeSpan.FromMilliseconds(100);

        public string AsDocument => string.Join('\n', History);

        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            string filename = $"{App.ProjectName}_{timestamp}.log";
            string location = Path.Combine(directory, filename);

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Logger already initialized");
                return;
            }

            Directory.CreateDirectory(directory);

            try
            {
                _filestream = File.Open(location, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            catch (UnauthorizedAccessException)
            {
                if (!NoWriteMode)
                {
                    WriteLine(LOG_IDENT, $"Cannot write to {directory}");
                    Frontend.ShowMessageBox(
                        string.Format(Strings.Logger_NoWriteMode, directory),
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxButton.OK
                    );
                }

                NoWriteMode = true;
                return;
            }

            Initialized = true;
            FileLocation = location;

            foreach (var entry in History)
                _logQueue.Enqueue(entry);

            _logTask = Task.Run(() => BackgroundWriterAsync(_cts.Token));

            WriteLine(LOG_IDENT, "Logger initialized");

            if (Paths.Initialized && Directory.Exists(Paths.Logs))
            {
                foreach (var log in new DirectoryInfo(Paths.Logs).GetFiles())
                {
                    if (log.LastWriteTimeUtc.AddDays(2) <= DateTime.UtcNow)
                    {
                        try { log.Delete(); }
                        catch (Exception ex) { WriteException(LOG_IDENT, ex); }
                    }
                }
            }
        }

        private async Task BackgroundWriterAsync(CancellationToken token)
        {
            var batch = new List<string>();
            var flushInterval = TimeSpan.FromSeconds(2);
            var lastFlush = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                while (_logQueue.TryDequeue(out var line))
                {
                    batch.Add(line);
                }

                if (batch.Count > 0 && (DateTime.UtcNow - lastFlush) >= flushInterval)
                {
                    try
                    {
                        await _semaphore.WaitAsync(token);
                        if (_filestream != null)
                        {
                            var combined = string.Join("\r\n", batch) + "\r\n";
                            byte[] data = Encoding.UTF8.GetBytes(combined);
                            await _filestream.WriteAsync(data, 0, data.Length, token);
                            await _filestream.FlushAsync(token);
                            batch.Clear();
                            lastFlush = DateTime.UtcNow;
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }

                await Task.Delay(250, token); // Less aggressive polling
            }

            // Final flush
            if (batch.Count > 0)
            {
                try
                {
                    await _semaphore.WaitAsync(token);
                    if (_filestream != null)
                    {
                        var combined = string.Join("\r\n", batch) + "\r\n";
                        byte[] data = Encoding.UTF8.GetBytes(combined);
                        await _filestream.WriteAsync(data, 0, data.Length, token);
                        await _filestream.FlushAsync(token);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }


        private void WriteLine(string message)
        {
            if (!IsLoggingEnabled) return;

            var now = DateTime.UtcNow;
            if ((now - _lastLogTime) < _logCooldown) return; // skip if too soon
            _lastLogTime = now;

            string timestamp = now.ToString("s") + "Z";
            string formatted = $"{timestamp} {message}";
            string sanitized = formatted.Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);

            Debug.WriteLine(sanitized);
            _logQueue.Enqueue(sanitized);
            History.Add(sanitized);

            if (History.Count > 1000)
                History.RemoveRange(0, History.Count - 1000);
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            var originalCulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            string hresult = $"0x{ex.HResult:X8}";
            WriteLine($"[{identifier}] ({hresult}) {ex}");

            Thread.CurrentThread.CurrentUICulture = originalCulture;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _logTask?.Wait();

            _filestream?.Dispose();
            _semaphore.Dispose();
            _cts.Dispose();
        }
    }
}
