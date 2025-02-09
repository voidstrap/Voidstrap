using Hellstrap.Models.SettingTasks.Base;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Hellstrap.Models.SettingTasks
{
    public class FontModPresetTask : StringBaseTask
    {
        public FontModPresetTask() : base("ModPreset", "TextFont")
        {
            if (File.Exists(Paths.CustomFont))
            {
                OriginalState = Paths.CustomFont;
            }
        }

        public string? GetFileHash()
        {
            if (!File.Exists(Paths.CustomFont))
            {
                return null;
            }

            using var fileStream = File.OpenRead(Paths.CustomFont);
            using var md5 = MD5.Create();
            return MD5Hash.Stringify(md5.ComputeHash(fileStream));
        }

        public override void Execute()
        {
            if (!string.IsNullOrEmpty(NewState) && !string.Equals(NewState, Paths.CustomFont, StringComparison.InvariantCultureIgnoreCase))
            {
                if (File.Exists(NewState))
                {
                    string? directoryPath = Path.GetDirectoryName(Paths.CustomFont);
                    if (directoryPath != null)
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    Filesystem.AssertReadOnly(Paths.CustomFont);
                    File.Copy(NewState, Paths.CustomFont, true);
                }
            }
            else if (File.Exists(Paths.CustomFont))
            {
                Filesystem.AssertReadOnly(Paths.CustomFont);
                File.Delete(Paths.CustomFont);
            }

            OriginalState = NewState;
        }
    }
}
