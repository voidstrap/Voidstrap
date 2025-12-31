using System.Security.Cryptography;
using System.Windows.Markup;

namespace Voidstrap.Models.Entities
{
    public class ModPresetFileData
    {
        public string FilePath { get; private set; }

        public string FullFilePath => Path.Combine(Paths.Mods, FilePath);

        public FileStream FileStream => File.OpenRead(FullFilePath);

        public string ResourceIdentifier { get; private set; }

        public Stream ResourceStream => Resource.GetStream(ResourceIdentifier);

        public byte[] ResourceHash { get; private set; }

        public ModPresetFileData(string contentPath, string resource)
        {
            FilePath = contentPath;
            ResourceIdentifier = resource;

            using var stream = ResourceStream;
            stream.Position = 0;
            ResourceHash = App.ComputeSha256(stream);
        }

        public bool HashMatches()
        {
            if (!File.Exists(FullFilePath))
                return false;

            using var fileStream = FileStream;
            fileStream.Position = 0;
            var fileHash = App.ComputeSha256(fileStream);

            return fileHash.SequenceEqual(ResourceHash);
        }
    }
}
