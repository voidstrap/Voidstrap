using System;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public static class BackgroundEvents
    {
        public static event Action<string>? BackgroundChanged;

        public static void RaiseBackgroundChanged(string path)
        {
            BackgroundChanged?.Invoke(path);
        }
    }
}
