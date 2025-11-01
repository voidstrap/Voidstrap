using System;
using System.Collections.Generic;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    /// <summary>
    /// Handles audio playback, queueing, and simple state management.
    /// Works for local files or direct stream URLs (.mp3, .wav, etc.).
    /// </summary>
    public sealed class PlaybackService : IDisposable
    {
        private WasapiOut? _outputDevice;
        private MediaFoundationReader? _reader;
        private readonly Queue<TrackItem> _queue = new();
        private readonly Random _rng = new();

        public bool IsPlaying { get; private set; }
        public bool Shuffle { get; set; }
        public bool Repeat { get; set; }

        private float _volume = 1.0f;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = (float)Math.Clamp(value, 0.0, 1.0);
                if (_outputDevice != null)
                    _outputDevice.Volume = _volume;
            }
        }

        public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
        public TrackItem? Current { get; private set; }

        public event EventHandler<TrackItem>? TrackChanged;
        public event EventHandler? PlayStateChanged;

        /// <summary>
        /// Starts playback for the given track.
        /// </summary>
        public void Play(TrackItem track)
        {
            StopInternal();

            if (track == null)
                return;
            if (!string.IsNullOrWhiteSpace(track.FilePath) && !File.Exists(track.FilePath))
                return;

            try
            {
                _reader = new MediaFoundationReader(track.FilePath);
                track.Duration = _reader.TotalTime;

                _outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 200);
                _outputDevice.Init(_reader);
                _outputDevice.Volume = _volume;

                _outputDevice.PlaybackStopped += (_, e) =>
                {
                    // automatically go to next if playback finishes naturally
                    if (e.Exception == null)
                        HandleTrackFinished();
                };

                _outputDevice.Play();
                IsPlaying = true;
                Current = track;

                TrackChanged?.Invoke(this, track);
                PlayStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback failed: {ex.Message}");
                StopInternal();
            }
        }

        public void PlayPause()
        {
            if (_outputDevice == null)
                return;

            if (IsPlaying)
            {
                _outputDevice.Pause();
                IsPlaying = false;
            }
            else
            {
                _outputDevice.Play();
                IsPlaying = true;
            }

            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Next()
        {
            var nextTrack = GetNextTrack();
            if (nextTrack != null)
                Play(nextTrack);
            else
                StopInternal();
        }

        public void Previous()
        {
            if (Position > TimeSpan.FromSeconds(3))
            {
                Seek(TimeSpan.Zero);
            }
            else if (Current != null)
            {
                Play(Current);
            }
        }

        public void Seek(TimeSpan t)
        {
            if (_reader == null)
                return;

            if (t < _reader.TotalTime)
                _reader.CurrentTime = t;
        }

        public void Enqueue(TrackItem track)
        {
            if (track != null)
                _queue.Enqueue(track);
        }

        public void ClearQueue() => _queue.Clear();

        // ==============================
        // == INTERNAL HELPERS ==
        // ==============================
        private void HandleTrackFinished()
        {
            if (Repeat && Current != null)
            {
                Play(Current);
                return;
            }

            var next = GetNextTrack();
            if (next != null)
            {
                Play(next);
            }
            else
            {
                StopInternal();
            }
        }

        private TrackItem? GetNextTrack()
        {
            if (_queue.Count == 0)
                return null;

            if (Shuffle)
            {
                var arr = _queue.ToArray();
                var index = _rng.Next(arr.Length);
                var t = arr[index];
                _queue.Clear();
                foreach (var x in arr)
                    if (x != t) _queue.Enqueue(x);
                return t;
            }

            return _queue.Dequeue();
        }

        private void StopInternal()
        {
            try { _outputDevice?.Stop(); } catch { }

            _outputDevice?.Dispose();
            _reader?.Dispose();
            _outputDevice = null;
            _reader = null;

            IsPlaying = false;
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            StopInternal();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Small extension helper for setting duration if you had a SetDuration() call earlier.
    /// </summary>
    public static class TrackItemExtensions
    {
        public static void SetDuration(this TrackItem item, TimeSpan dur)
        {
            item.Duration = dur;
        }
    }
}
