using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Voidstrap.Extensions
{
    public static class ThemeEx
    {
        public static Theme GetFinal(this Theme dialogTheme)
        {
            if (dialogTheme != Theme.Default)
                return dialogTheme;

            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value && value == 0)
                return Theme.Dark;

            return Theme.Light;
        }

        public static IReadOnlyCollection<Theme> Selections => new[]
        {
            Theme.Default,
            Theme.Dark,
            Theme.Light,
            Theme.Voidstrap,
            Theme.Blue,
            Theme.Cyan,
            Theme.Green,
            Theme.Orange,
            Theme.Pink,
            Theme.Red,
            Theme.Yellow
        };
    }
}
