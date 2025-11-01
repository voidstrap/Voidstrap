// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Interop;

namespace Wpf.Ui.Appearance
{
    /// <summary>
    /// Lets you apply background effects to <see cref="Window"/> or <c>hWnd</c> by its <see cref="IntPtr"/>.
    /// </summary>
    public static class Background
    {
        /// <summary>
        /// Checks if the current <see cref="Windows"/> supports selected <see cref="BackgroundType"/>.
        /// Modified: allows Aero/glass fallback even on Windows 10.
        /// </summary>
        public static bool IsSupported(BackgroundType type)
        {
            return type switch
            {
                BackgroundType.Auto => Win32.Utilities.IsOSWindows7OrNewer,
                BackgroundType.Tabbed => Win32.Utilities.IsOSWindows11Insider1OrNewer,
                BackgroundType.Mica => Win32.Utilities.IsOSWindows7OrNewer,
                BackgroundType.Acrylic => Win32.Utilities.IsOSWindows7OrNewer,
                BackgroundType.Aero => Win32.Utilities.IsOSWindows7OrNewer,
                BackgroundType.Unknown => true,
                BackgroundType.None => true,
                BackgroundType.Disable => false,
                _ => false
            };
        }

        public static bool Apply(Window window, BackgroundType type)
            => Apply(window, type, false);

        public static bool Apply(Window window, BackgroundType type, bool force)
        {
            if (!force && !IsSupported(type))
                return false;

            if (window.IsLoaded)
            {
                var windowHandle = new WindowInteropHelper(window).Handle;
                if (windowHandle == IntPtr.Zero)
                    return false;

                RemoveContentBackground(window);
                return Apply(windowHandle, type, force);
            }

            window.Loaded += (sender, _) =>
            {
                var windowHandle = new WindowInteropHelper(sender as Window).Handle;
                if (windowHandle == IntPtr.Zero)
                    return;

                RemoveContentBackground(sender as Window);
                Apply(windowHandle, type, force);
            };

            return true;
        }

        public static bool Apply(IntPtr handle, BackgroundType type)
            => Apply(handle, type, false);

        public static bool Apply(IntPtr handle, BackgroundType type, bool force)
        {
            if (!force && !IsSupported(type))
                return false;

            if (!force && !UnsafeNativeMethods.IsCompositionEnabled())
                return false;

            if (handle == IntPtr.Zero)
                return false;

            if (type == BackgroundType.Unknown || type == BackgroundType.None)
            {
                Remove(handle);
                return true;
            }

            if (Theme.GetAppTheme() == ThemeType.Dark)
                UnsafeNativeMethods.ApplyWindowDarkMode(handle);
            else
                UnsafeNativeMethods.RemoveWindowDarkMode(handle);

            // Remove caption color/titlebar background for consistency
            UnsafeNativeMethods.RemoveWindowCaption(handle);

            AppearanceData.AddHandle(handle);

            // Always allow Aero effect fallback
            if (type == BackgroundType.Aero)
                return UnsafeNativeMethods.ApplyWindowAeroEffect(handle);

            // ---- MODIFIED SECTION ----
            // First release of Windows 11
            if (!Win32.Utilities.IsOSWindows11Insider1OrNewer)
            {
                // Windows 10 & older fallback
                if (type == BackgroundType.Mica || type == BackgroundType.Auto)
                {
                    // Prefer legacy mica if implemented, else Aero glass
                    if (!UnsafeNativeMethods.ApplyWindowLegacyMicaEffect(handle))
                        return UnsafeNativeMethods.ApplyWindowAeroEffect(handle);
                    return true;
                }

                if (type == BackgroundType.Acrylic)
                {
                    // Try legacy acrylic, fallback to Aero
                    if (!UnsafeNativeMethods.ApplyWindowLegacyAcrylicEffect(handle))
                        return UnsafeNativeMethods.ApplyWindowAeroEffect(handle);
                    return true;
                }

                // Force glass fallback on anything else
                return UnsafeNativeMethods.ApplyWindowAeroEffect(handle);
            }

            // Newer Windows 11 versions
            return UnsafeNativeMethods.ApplyWindowBackdrop(handle, type);
        }

        public static bool Remove(Window window)
        {
            if (window == null)
                return false;

            var windowHandle = new WindowInteropHelper(window).Handle;
            RestoreContentBackground(window);

            if (windowHandle == IntPtr.Zero)
                return false;

            UnsafeNativeMethods.RemoveWindowBackdrop(windowHandle);

            if (AppearanceData.HasHandle(windowHandle))
                AppearanceData.RemoveHandle(windowHandle);

            return true;
        }

        public static bool Remove(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return false;

            RestoreContentBackground(handle);
            UnsafeNativeMethods.RemoveWindowBackdrop(handle);

            if (AppearanceData.HasHandle(handle))
                AppearanceData.RemoveHandle(handle);

            return true;
        }

        public static bool RemoveContentBackground(Window window)
        {
            if (window == null)
                return false;

            window.Background = Brushes.Transparent;
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
                return false;

            var windowSource = HwndSource.FromHwnd(windowHandle);
            if (windowSource?.Handle != IntPtr.Zero && windowSource?.CompositionTarget != null)
                windowSource.CompositionTarget.BackgroundColor = Colors.Transparent;

            return true;
        }

        public static bool RestoreContentBackground(Window window)
        {
            if (window == null)
                return false;

            var backgroundBrush = Application.Current.Resources["ApplicationBackgroundBrush"];
            if (backgroundBrush is not SolidColorBrush)
                backgroundBrush = window.Resources["ApplicationBackgroundBrush"];

            if (backgroundBrush is not SolidColorBrush)
                backgroundBrush = Theme.GetAppTheme() == ThemeType.Dark
                    ? new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20))
                    : new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));

            window.Background = (SolidColorBrush)backgroundBrush;

            var windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
                return false;

            var windowSource = HwndSource.FromHwnd(windowHandle);
            Appearance.Background.Remove(windowHandle);

            if (windowSource?.Handle != IntPtr.Zero && windowSource?.CompositionTarget != null)
                windowSource.CompositionTarget.BackgroundColor = SystemColors.WindowColor;

            return true;
        }

        public static bool RestoreContentBackground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            if (!UnsafeNativeMethods.IsValidWindow(hWnd))
                return false;

            var windowSource = HwndSource.FromHwnd(hWnd);
            if (windowSource?.Handle != IntPtr.Zero && windowSource?.CompositionTarget != null)
                windowSource.CompositionTarget.BackgroundColor = SystemColors.WindowColor;

            if (windowSource?.RootVisual is Window window)
            {
                var backgroundBrush = window.Resources["ApplicationBackgroundBrush"];
                if (backgroundBrush is not SolidColorBrush)
                    backgroundBrush = Theme.GetAppTheme() == ThemeType.Dark
                        ? new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20))
                        : new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));

                window.Background = (SolidColorBrush)backgroundBrush;
            }

            return true;
        }

        internal static void RemoveAll()
        {
            var handles = AppearanceData.ModifiedBackgroundHandles;
            foreach (var singleHandle in handles)
            {
                if (!UnsafeNativeMethods.IsValidWindow(singleHandle))
                    continue;

                Remove(singleHandle);
                AppearanceData.RemoveHandle(singleHandle);
            }
        }

        internal static void UpdateAll(ThemeType themeType, BackgroundType backdropType = BackgroundType.Unknown)
        {
            var handles = AppearanceData.ModifiedBackgroundHandles;

            foreach (var singleHandle in handles)
            {
                if (!UnsafeNativeMethods.IsValidWindow(singleHandle))
                    continue;

                if (themeType == ThemeType.Dark)
                    UnsafeNativeMethods.ApplyWindowDarkMode(singleHandle);
                else
                    UnsafeNativeMethods.RemoveWindowDarkMode(singleHandle);

                if (Win32.Utilities.IsOSWindows11Insider1OrNewer)
                {
                    if (!UnsafeNativeMethods.IsWindowHasBackdrop(singleHandle, backdropType))
                        UnsafeNativeMethods.ApplyWindowBackdrop(singleHandle, backdropType);

                    continue;
                }

                if (backdropType == BackgroundType.Mica)
                {
                    if (!UnsafeNativeMethods.IsWindowHasLegacyMica(singleHandle))
                        UnsafeNativeMethods.ApplyWindowLegacyMicaEffect(singleHandle);
                }
                else if (backdropType == BackgroundType.Acrylic)
                {
                    // Just reapply legacy acrylic if possible (safe even if already applied)
                    UnsafeNativeMethods.ApplyWindowLegacyAcrylicEffect(singleHandle);
                }
                else
                {
                    // Always allow fallback Aero blur
                    UnsafeNativeMethods.ApplyWindowAeroEffect(singleHandle);
                }
            }
        }
    }
}
