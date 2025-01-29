// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, you can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Wpf.Ui.Appearance;

internal static class SystemTheme
{
    /// <summary>
    /// Gets the current main color of the system.
    /// </summary>
    public static Color GlassColor => SystemParameters.WindowGlassColor;

    /// <summary>
    /// Determines whether the system is currently set to high contrast mode.
    /// </summary>
    public static bool HighContrast => SystemParameters.HighContrast;

    /// <summary>
    /// Gets the currently set system theme based on <see cref="Registry"/> value.
    /// </summary>
    public static SystemThemeType GetTheme()
    {
        var currentTheme =
            Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes",
                "CurrentTheme", "aero.theme") as string ?? string.Empty;

        if (string.IsNullOrEmpty(currentTheme))
            return SystemThemeType.Unknown;

        currentTheme = currentTheme.ToLower().Trim();

        // Check for known theme types
        if (currentTheme.Contains("basic.theme"))
            return SystemThemeType.Light;

        if (currentTheme.Contains("aero.theme"))
            return SystemThemeType.Light;

        if (currentTheme.Contains("dark.theme"))
            return SystemThemeType.Dark;

        if (currentTheme.Contains("themea.theme"))
            return SystemThemeType.Glow;

        if (currentTheme.Contains("themeb.theme"))
            return SystemThemeType.CapturedMotion;

        if (currentTheme.Contains("themec.theme"))
            return SystemThemeType.Sunrise;

        if (currentTheme.Contains("themed.theme"))
            return SystemThemeType.Flow;

        // Check for light/dark theme preference based on registry
        var rawAppsUseLightTheme = Registry.GetValue(
            "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
            "AppsUseLightTheme", 1);

        // If the value is explicitly set to 0, return dark theme
        if (rawAppsUseLightTheme is int appsUseLightTheme && appsUseLightTheme == 0)
            return SystemThemeType.Dark;

        var rawSystemUsesLightTheme = Registry.GetValue(
            "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
            "SystemUsesLightTheme", 1);

        // If the value is explicitly set to 0, return dark theme
        return rawSystemUsesLightTheme is int systemUsesLightTheme && systemUsesLightTheme == 0
            ? SystemThemeType.Dark
            : SystemThemeType.Light;
    }
}
