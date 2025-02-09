using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hellstrap
{
    static class Resource
    {
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        private static readonly string[] ResourceNames = Assembly.GetManifestResourceNames();

        public static Stream? GetStream(string name)
        {
            string? path = ResourceNames.AsParallel().FirstOrDefault(str => str.EndsWith(name, StringComparison.OrdinalIgnoreCase));

            if (path == null)
                throw new FileNotFoundException($"Resource '{name}' not found in assembly.");

            Stream? stream = Assembly.GetManifestResourceStream(path);
            return stream ?? throw new InvalidOperationException($"Failed to load resource '{name}'.");
        }

        public static async Task<byte[]> Get(string name)
        {
            using var stream = GetStream(name);
            if (stream == null) throw new InvalidOperationException($"Resource stream '{name}' is null.");

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        public static async Task<string> GetString(string name)
        {
            byte[] data = await Get(name);
            return Encoding.UTF8.GetString(data);
        }
    }
}
