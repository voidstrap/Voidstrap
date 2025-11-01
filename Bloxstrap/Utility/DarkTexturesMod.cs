using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Voidstrap;

namespace Voidstrap
{
    public class DarkTexturesInstaller
    {
        private static readonly string DownloadUrl = "https://cocajola.com/wp-content/uploads/2024/09/dark-textures-rivals.zip";

        public static async Task DownloadAndExtractAsync()
        {
            string modsPath = Path.Combine(Paths.Mods, "PlatformContent", "pc", "textures");
            Directory.CreateDirectory(modsPath);

            string tempZip = Path.Combine(Path.GetTempPath(), "dark-textures-rivals.zip");
            using (HttpClient client = new HttpClient())
            {
                var data = await client.GetByteArrayAsync(DownloadUrl);
                await File.WriteAllBytesAsync(tempZip, data);
            }
            string tempExtractPath = Path.Combine(Path.GetTempPath(), "dark-textures-temp");
            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtractPath);
            string topFolder = Directory.GetDirectories(tempExtractPath)[0];
            foreach (var dir in Directory.GetDirectories(topFolder))
            {
                string destDir = Path.Combine(modsPath, Path.GetFileName(dir));
                if (Directory.Exists(destDir))
                {
                    foreach (var subDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(dir, subDir);
                        Directory.CreateDirectory(Path.Combine(destDir, relativePath));
                    }

                    foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(dir, file);
                        string destFile = Path.Combine(destDir, relativePath);
                        File.Copy(file, destFile, true);
                    }
                }
                else
                {
                    Directory.Move(dir, destDir);
                }
            }

            foreach (var file in Directory.GetFiles(topFolder))
            {
                string destFile = Path.Combine(modsPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            File.Delete(tempZip);
            Directory.Delete(tempExtractPath, true);
        }
    }
}
