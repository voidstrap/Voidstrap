using System;
using System.Threading;
using System.Threading.Tasks;

namespace Voidstrap.Utility
{
    public sealed class AsyncMutex : IAsyncDisposable
    {
        private readonly bool _initiallyOwned;
        private readonly string _name;
        private Task? _mutexTask;
        private ManualResetEventSlim? _releaseEvent;
        private CancellationTokenSource? _cts;
        public AsyncMutex(bool initiallyOwned, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Mutex name cannot be null or whitespace.", nameof(name));
            _initiallyOwned = initiallyOwned;
            _name = name;
        }

        public Task AcquireAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseEvent = new ManualResetEventSlim(false);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _mutexTask = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        using var mutex = new Mutex(_initiallyOwned, _name);
                        var token = _cts.Token;

                        try
                        {
                            int signaledIndex = WaitHandle.WaitAny(new[] { mutex, token.WaitHandle });
                            if (signaledIndex != 0)
                            {
                                tcs.TrySetCanceled(token);
                                return;
                            }
                        }
                        catch (AbandonedMutexException)
                        {
                        }

                        tcs.TrySetResult();
                        _releaseEvent.Wait(token);

                        mutex.ReleaseMutex();
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(_cts.Token);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            return tcs.Task;
        }

        public async Task ReleaseAsync()
        {
            _releaseEvent?.Set();

            if (_mutexTask is not null)
                await _mutexTask.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts?.Cancel();
                await ReleaseAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _releaseEvent?.Dispose();
                _cts?.Dispose();
            }
        }
    }
}
