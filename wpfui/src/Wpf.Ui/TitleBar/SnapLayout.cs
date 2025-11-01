// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Dpi;

namespace Wpf.Ui.TitleBar
{
    /// <summary>
    /// Enables Windows 11 Snap Layout functionality for a custom <see cref="Controls.TitleBar"/>.
    /// </summary>
    internal sealed class SnapLayout : IThemeControl
    {
        private readonly SnapLayoutButton[] _buttons;
        private ThemeType _currentTheme;
        private SolidColorBrush _currentHoverColor = Brushes.Transparent;

        /// <summary>
        /// Gets or sets the current theme.
        /// </summary>
        public ThemeType Theme
        {
            get => _currentTheme;
            set
            {
                _currentTheme = value;
                _currentHoverColor = value == ThemeType.Light ? HoverColorLight : HoverColorDark;
            }
        }

        /// <summary>
        /// Default button background.
        /// </summary>
        public SolidColorBrush DefaultButtonBackground { get; set; } = Brushes.Transparent;

        /// <summary>
        /// Hover color for light theme.
        /// </summary>
        public SolidColorBrush HoverColorLight { get; set; } = Brushes.Transparent;

        /// <summary>
        /// Hover color for dark theme.
        /// </summary>
        public SolidColorBrush HoverColorDark { get; set; } = Brushes.Transparent;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapLayout"/> class.
        /// </summary>
        private SnapLayout(Window window, Wpf.Ui.Controls.Button maximizeButton, Wpf.Ui.Controls.Button restoreButton)
        {
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var dpiScale = DpiHelper.GetWindowDpi(hwnd).DpiScaleX;

            _buttons = new[]
            {
                new SnapLayoutButton(maximizeButton, TitleBarButton.Maximize, dpiScale),
                new SnapLayoutButton(restoreButton, TitleBarButton.Restore, dpiScale)
            };

            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }

        /// <summary>
        /// Checks if Snap Layouts are supported on the current OS.
        /// </summary>
        public static bool IsSupported() => Win32.Utilities.IsOSWindows11OrNewer;

        /// <summary>
        /// Registers Snap Layout for the provided window and title bar buttons.
        /// </summary>
        public static SnapLayout Register(Window window, Wpf.Ui.Controls.Button maximizeButton, Wpf.Ui.Controls.Button restoreButton)
            => new(window, maximizeButton, restoreButton);

        /// <summary>
        /// Processes native Win32 window messages.
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var message = (Interop.User32.WM)msg;

            switch (message)
            {
                case Interop.User32.WM.MOVE:
                    // Reserved for future DPI scaling updates.
                    break;

                case Interop.User32.WM.NCMOUSELEAVE:
                    RemoveHoverFromAll();
                    break;

                case Interop.User32.WM.NCLBUTTONDOWN:
                    HandleMouseDown(lParam, ref handled);
                    break;

                case Interop.User32.WM.NCLBUTTONUP:
                    HandleMouseUp(lParam, ref handled);
                    break;

                case Interop.User32.WM.NCHITTEST:
                    return HandleHitTest(lParam, ref handled);
            }

            return IntPtr.Zero;
        }

        private void RemoveHoverFromAll()
        {
            foreach (var btn in _buttons)
                btn.RemoveHover(DefaultButtonBackground);
        }

        private void HandleMouseDown(IntPtr lParam, ref bool handled)
        {
            foreach (var btn in _buttons)
            {
                if (!btn.IsMouseOver(lParam)) continue;
                btn.IsClickedDown = true;
                handled = true;
            }
        }

        private void HandleMouseUp(IntPtr lParam, ref bool handled)
        {
            foreach (var btn in _buttons)
            {
                if (btn.IsClickedDown && btn.IsMouseOver(lParam))
                {
                    btn.InvokeClick();
                    handled = true;
                    break;
                }
            }
        }

        private IntPtr HandleHitTest(IntPtr lParam, ref bool handled)
        {
            foreach (var btn in _buttons)
            {
                if (btn.IsMouseOver(lParam))
                {
                    btn.Hover(_currentHoverColor);
                    handled = true;
                    return new IntPtr((int)Interop.User32.WM_NCHITTEST.HTMAXBUTTON);
                }

                btn.RemoveHover(DefaultButtonBackground);
            }

            return IntPtr.Zero;
        }
    }
}
