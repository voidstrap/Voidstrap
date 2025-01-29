using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Extensions;
using Wpf.Ui.Interop;

namespace Wpf.Ui.Appearance
{
    /// <summary>
    /// Handles application accent color management.
    /// </summary>
    public static class Accent
    {
        private const double BackgroundBrightnessThreshold = 80d;

        public static Color SystemAccent => GetResourceColor("SystemAccentColor");
        public static Brush SystemAccentBrush => SystemAccent.ToBrush();

        public static Color PrimaryAccent => GetResourceColor("SystemAccentColorPrimary");
        public static Brush PrimaryAccentBrush => PrimaryAccent.ToBrush();

        public static Color SecondaryAccent => GetResourceColor("SystemAccentColorSecondary");
        public static Brush SecondaryAccentBrush => SecondaryAccent.ToBrush();

        public static Color TertiaryAccent => GetResourceColor("SystemAccentColorTertiary");
        public static Brush TertiaryAccentBrush => TertiaryAccent.ToBrush();

        public static void Apply(Color systemAccent, ThemeType themeType = ThemeType.Light, bool useGlassColor = false)
        {
            if (useGlassColor)
                systemAccent = systemAccent.UpdateBrightness(7f);

            var (primary, secondary, tertiary) = themeType == ThemeType.Dark
                ? (systemAccent.Update(15f, -12f), systemAccent.Update(30f, -24f), systemAccent.Update(45f, -36f))
                : (systemAccent.UpdateBrightness(-5f), systemAccent.UpdateBrightness(-10f), systemAccent.UpdateBrightness(-15f));

            UpdateColorResources(systemAccent, primary, secondary, tertiary);
        }

        public static void Apply(Color systemAccent, Color primaryAccent, Color secondaryAccent, Color tertiaryAccent)
        {
            UpdateColorResources(systemAccent, primaryAccent, secondaryAccent, tertiaryAccent);
        }

        public static void ApplySystemAccent()
        {
            Apply(GetColorizationColor(), Theme.GetAppTheme());
        }

        public static Color GetColorizationColor() => UnsafeNativeMethods.GetDwmColor();

        private static Color GetResourceColor(string resourceKey)
        {
            return Application.Current.Resources[resourceKey] is Color color ? color : Colors.Transparent;
        }

        private static void UpdateColorResources(Color systemAccent, Color primaryAccent, Color secondaryAccent, Color tertiaryAccent)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"INFO | Updating accent colors:\n  - SystemAccent: {systemAccent}\n  - Primary: {primaryAccent}\n  - Secondary: {secondaryAccent}\n  - Tertiary: {tertiaryAccent}", "Wpf.Ui.Accent");
#endif
            bool isDarkText = secondaryAccent.GetBrightness() > BackgroundBrightnessThreshold;
            SetTextColors(isDarkText);

            Application.Current.Resources["SystemAccentColor"] = systemAccent;
            Application.Current.Resources["SystemAccentColorPrimary"] = primaryAccent;
            Application.Current.Resources["SystemAccentColorSecondary"] = secondaryAccent;
            Application.Current.Resources["SystemAccentColorTertiary"] = tertiaryAccent;

            var secondaryBrush = secondaryAccent.ToBrush();
            var tertiaryBrush = tertiaryAccent.ToBrush();

            Application.Current.Resources["SystemAccentBrush"] = secondaryBrush;
            Application.Current.Resources["SystemFillColorAttentionBrush"] = secondaryBrush;
            Application.Current.Resources["AccentTextFillColorPrimaryBrush"] = tertiaryBrush;
            Application.Current.Resources["AccentTextFillColorSecondaryBrush"] = tertiaryBrush;
            Application.Current.Resources["AccentTextFillColorTertiaryBrush"] = secondaryBrush;
            Application.Current.Resources["AccentFillColorSelectedTextBackgroundBrush"] = systemAccent.ToBrush();
            Application.Current.Resources["AccentFillColorDefaultBrush"] = secondaryBrush;


        }

        private static void SetTextColors(bool isDarkText)
        {
            byte alphaPrimary = 0xFF, alphaSecondary = 0x80, alphaDisabled = 0x77, alphaSelectedText = 0xFF, alphaAccentDisabled = 0x5D;
            byte colorValue = isDarkText ? (byte)0x00 : (byte)0xFF;

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"INFO | Text on accent is {(isDarkText ? "DARK" : "LIGHT")}", "Wpf.Ui.Accent");
#endif

            Application.Current.Resources["TextOnAccentFillColorPrimary"] = Color.FromArgb(alphaPrimary, colorValue, colorValue, colorValue);
            Application.Current.Resources["TextOnAccentFillColorSecondary"] = Color.FromArgb(alphaSecondary, colorValue, colorValue, colorValue);
            Application.Current.Resources["TextOnAccentFillColorDisabled"] = Color.FromArgb(alphaDisabled, colorValue, colorValue, colorValue);
            Application.Current.Resources["TextOnAccentFillColorSelectedText"] = Color.FromArgb(alphaSelectedText, colorValue, colorValue, colorValue);
            Application.Current.Resources["AccentTextFillColorDisabled"] = Color.FromArgb(alphaAccentDisabled, colorValue, colorValue, colorValue);
        }
    }
}
