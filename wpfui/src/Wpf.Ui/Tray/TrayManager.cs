// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, 
// You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski
// and WPF UI Contributors. All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Interop;

namespace Wpf.Ui.Tray
{
    /*
     * TODO: Handle closing of the parent window.
     * 
     * ISSUE:
     * If the main window is closed via Debugger or forcibly destroyed, 
     * it may not send WM_CLOSE or WM_DESTROY messages to child windows.
     * As a result, the tray icon may persist after the application exits.
     *
     * SUGGESTED FIX:
     * Implement a detection mechanism in TrayHandler to monitor whether
     * the parent window still exists. If the parent is gone, explicitly call:
     * 
     *   Shell32.Shell_NotifyIcon(Shell32.NIM.DELETE, Shell32.NOTIFYICONDATA)
     *
     * Similarly, detect unexpected TrayHandler disposal and remove icons
     * to prevent orphaned tray icons.
     */

    /// <summary>
    /// Manages system tray icons for WPF applications.
    /// </summary>
    internal static class TrayManager
    {
        /// <summary>
        /// Registers a tray icon using the application's main window as parent.
        /// </summary>
        public static bool Register(INotifyIcon notifyIcon)
        {
            return Register(notifyIcon, GetParentSource());
        }

        /// <summary>
        /// Registers a tray icon using a specified <see cref="Window"/> as parent.
        /// </summary>
        public static bool Register(INotifyIcon notifyIcon, Window parentWindow)
        {
            if (parentWindow is null)
                return false;

            return Register(notifyIcon, PresentationSource.FromVisual(parentWindow) as HwndSource);
        }

        /// <summary>
        /// Registers a tray icon using a specified <see cref="HwndSource"/>.
        /// </summary>
        public static bool Register(INotifyIcon notifyIcon, HwndSource parentSource)
        {
            if (notifyIcon is null)
                throw new ArgumentNullException(nameof(notifyIcon));

            if (parentSource is null)
            {
                if (notifyIcon.IsRegistered)
                    Unregister(notifyIcon);
                return false;
            }

            if (parentSource.Handle == IntPtr.Zero)
                return false;

            // Ensure clean re-registration
            if (notifyIcon.IsRegistered)
                Unregister(notifyIcon);

            notifyIcon.Id = TrayData.NotifyIcons.Count + 1;
            notifyIcon.HookWindow = new TrayHandler(
                $"wpfui_th_{parentSource.Handle}_{notifyIcon.Id}",
                parentSource.Handle)
            {
                ElementId = notifyIcon.Id
            };

            // Prepare NOTIFYICONDATA
            notifyIcon.ShellIconData = new Interop.Shell32.NOTIFYICONDATA
            {
                uID = notifyIcon.Id,
                uFlags = Interop.Shell32.NIF.MESSAGE,
                uCallbackMessage = (int)Interop.User32.WM.TRAYMOUSEMESSAGE,
                hWnd = notifyIcon.HookWindow.Handle,
                dwState = 0x2
            };

            // Set tooltip text
            if (!string.IsNullOrWhiteSpace(notifyIcon.TooltipText))
            {
                notifyIcon.ShellIconData.szTip = notifyIcon.TooltipText;
                notifyIcon.ShellIconData.uFlags |= Interop.Shell32.NIF.TIP;
            }

            // Set icon handle
            var hIcon = notifyIcon.Icon != null
                ? Hicon.FromSource(notifyIcon.Icon)
                : Hicon.FromApp();

            if (hIcon != IntPtr.Zero)
            {
                notifyIcon.ShellIconData.hIcon = hIcon;
                notifyIcon.ShellIconData.uFlags |= Interop.Shell32.NIF.ICON;
            }

            // Add window hook
            notifyIcon.HookWindow.AddHook(notifyIcon.WndProc);

            // Add to system tray
            Interop.Shell32.Shell_NotifyIcon(Interop.Shell32.NIM.ADD, notifyIcon.ShellIconData);

            TrayData.NotifyIcons.Add(notifyIcon);
            notifyIcon.IsRegistered = true;

            return true;
        }

        /// <summary>
        /// Unregisters and removes a tray icon from the system tray.
        /// </summary>
        public static bool Unregister(INotifyIcon notifyIcon)
        {
            if (notifyIcon is null)
                throw new ArgumentNullException(nameof(notifyIcon));

            if (!notifyIcon.IsRegistered || notifyIcon.ShellIconData is null)
                return false;

            Interop.Shell32.Shell_NotifyIcon(Interop.Shell32.NIM.DELETE, notifyIcon.ShellIconData);

            notifyIcon.IsRegistered = false;
            notifyIcon.HookWindow?.Dispose();
            notifyIcon.HookWindow = null;

            TrayData.NotifyIcons.Remove(notifyIcon);

            return true;
        }

        /// <summary>
        /// Retrieves the main application's <see cref="HwndSource"/>.
        /// </summary>
        private static HwndSource? GetParentSource()
        {
            var mainWindow = Application.Current?.MainWindow;
            return mainWindow is null
                ? null
                : PresentationSource.FromVisual(mainWindow) as HwndSource;
        }
    }
}
