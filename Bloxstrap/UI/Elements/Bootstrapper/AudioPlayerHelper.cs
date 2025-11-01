using System;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public static class AudioPlayerHelper
    {
        private static readonly MediaPlayer _player = new();

        public static void PlayStartupAudio()
        {
            try
            {
                string? audioFile = Directory.GetFiles(Paths.Base, "startup_audio.*").FirstOrDefault();
                if (audioFile is null)
                    return;

                _player.Stop();
                _player.Open(new Uri(audioFile, UriKind.Absolute));
                _player.Volume = 0.3;
                _player.Play();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AudioPlayerHelper::PlayStartupAudio", ex);
            }
        }

        public static void StopAudio()
        {
            try
            {
                _player.Stop();
                _player.Close();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AudioPlayerHelper::StopAudio", ex);
            }
        }
    }
}
