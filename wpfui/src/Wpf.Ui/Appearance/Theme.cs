// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, you can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using Wpf.Ui.Interop;

namespace Wpf.Ui.Appearance
{
    /// <summary>
    /// Allows managing available color themes from the library.
    /// </summary>
    public static class Theme
    {
        /// <summary>
        /// Event triggered when the application's theme is changed.
        /// </summary>
        public static event ThemeChangedEvent Changed;

        /// <summary>
        /// Gets a value that indicates whether the application is currently using the high contrast theme.
        /// </summary>
        public static bool IsHighContrast() => AppearanceData.ApplicationTheme == ThemeType.HighContrast;

        /// <summary>
        /// Gets a value that indicates whether Windows is currently using the high contrast theme.
        /// </summary>
        public static bool IsSystemHighContrast() => SystemTheme.HighContrast;

        /// <summary>
        /// Changes the current application theme.
        /// </summary>
        public static void Apply(ThemeType themeType, BackgroundType backgroundEffect = BackgroundType.Mica,
            bool updateAccent = true, bool forceBackground = false)
        {
            if (themeType == ThemeType.Unknown || themeType == AppearanceData.ApplicationTheme)
                return;

            // Apply accent if requested
            if (updateAccent)
                Accent.Apply(Accent.GetColorizationColor(), themeType, false);

            // Update theme dictionary
            var themeDictionaryName = themeType == ThemeType.Dark ? "Dark" : "Light";
            var appDictionaries = new ResourceDictionaryManager(AppearanceData.LibraryNamespace);
            var isUpdated = appDictionaries.UpdateDictionary("theme", new Uri(AppearanceData.LibraryThemeDictionariesUri + themeDictionaryName + ".xaml", UriKind.Absolute));

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"INFO | {typeof(Theme)} tries to update theme to {themeDictionaryName} ({themeType}): {isUpdated}", "Wpf.Ui.Theme");
#endif

            if (!isUpdated)
                return;

            AppearanceData.ApplicationTheme = themeType;
            Changed?.Invoke(themeType, Accent.SystemAccent);

            // Update background after theme change
            UpdateBackground(themeType, backgroundEffect, forceBackground);
        }

        /// <summary>
        /// Gets the currently set application theme.
        /// </summary>
        public static ThemeType GetAppTheme()
        {
            if (AppearanceData.ApplicationTheme == ThemeType.Unknown)
                FetchApplicationTheme();

            return AppearanceData.ApplicationTheme;
        }

        /// <summary>
        /// Gets the currently set system theme.
        /// </summary>
        public static SystemThemeType GetSystemTheme()
        {
            if (AppearanceData.SystemTheme == SystemThemeType.Unknown)
                FetchSystemTheme();

            return AppearanceData.SystemTheme;
        }

        /// <summary>
        /// Gets a value that indicates whether the application matches the system theme.
        /// </summary>
        public static bool IsAppMatchesSystem()
        {
            var appTheme = GetAppTheme();
            var sysTheme = GetSystemTheme();

            // Convert SystemThemeType to ThemeType or compare equivalent values
            switch (sysTheme)
            {
                case SystemThemeType.Dark:
                case SystemThemeType.CapturedMotion:
                case SystemThemeType.Glow:
                    return appTheme == ThemeType.Dark;

                case SystemThemeType.Light:
                case SystemThemeType.Flow:
                case SystemThemeType.Sunrise:
                    return appTheme == ThemeType.Light;

                default:
                    return appTheme == ThemeType.HighContrast && SystemTheme.HighContrast;
            }
        }


        /// <summary>
        /// Tries to remove dark theme from <see cref="Window"/>.
        /// </summary>
        public static bool RemoveDarkThemeFromWindow(Window window)
        {
            if (window == null)
                return false;

            if (window.IsLoaded)
                return UnsafeNativeMethods.RemoveWindowDarkMode(window);

            window.Loaded += (sender, _) => UnsafeNativeMethods.RemoveWindowDarkMode(sender as Window);

            return true;
        }

        /// <summary>
        /// Tries to guess the currently set application theme.
        /// </summary>
        private static void FetchApplicationTheme()
        {
            var appDictionaries = new ResourceDictionaryManager(AppearanceData.LibraryNamespace);
            var themeDictionary = appDictionaries.GetDictionary("theme");

            if (themeDictionary == null)
                return;

            var themeUri = themeDictionary.Source.ToString().Trim().ToLower();

            if (themeUri.Contains("light"))
                AppearanceData.ApplicationTheme = ThemeType.Light;
            else if (themeUri.Contains("dark"))
                AppearanceData.ApplicationTheme = ThemeType.Dark;
            else if (themeUri.Contains("highcontrast"))
                AppearanceData.ApplicationTheme = ThemeType.HighContrast;
        }

        /// <summary>
        /// Tries to guess the currently set system theme.
        /// </summary>
        private static void FetchSystemTheme() => AppearanceData.SystemTheme = SystemTheme.GetTheme();

        /// <summary>
        /// Forces change to application background if a custom background effect was applied.
        /// </summary>
        private static void UpdateBackground(ThemeType themeType, BackgroundType backgroundEffect = BackgroundType.Unknown, bool forceBackground = false)
        {
            Background.UpdateAll(themeType, backgroundEffect);

            var mainWindow = Application.Current.MainWindow;

            if (mainWindow == null || !AppearanceData.HasHandle(mainWindow))
                return;

            Background.Apply(mainWindow, backgroundEffect, forceBackground);
        }

        private static bool IsSystemDarkTheme() =>
            GetSystemTheme() is SystemThemeType.Dark or SystemThemeType.CapturedMotion or SystemThemeType.Glow;

        private static bool IsSystemLightTheme() =>
            GetSystemTheme() is SystemThemeType.Light or SystemThemeType.Flow or SystemThemeType.Sunrise;
    }
}
