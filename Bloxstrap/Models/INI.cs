namespace Voidstrap.Models
{
    public static class IniFile
    {
        public static Dictionary<string, string> Read(string path)
        {
            var dict = new Dictionary<string, string>();
            if (!File.Exists(path)) return dict;

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[") || line.StartsWith(";"))
                    continue;

                var split = line.Split('=', 2);
                if (split.Length == 2)
                    dict[split[0].Trim()] = split[1].Trim();
            }
            return dict;
        }

        public static void Write(string path, Dictionary<string, string> data)
        {
            using var sw = new StreamWriter(path);
            sw.WriteLine("[Crosshair]");
            foreach (var kv in data)
                sw.WriteLine($"{kv.Key}={kv.Value}");
        }
    }
}