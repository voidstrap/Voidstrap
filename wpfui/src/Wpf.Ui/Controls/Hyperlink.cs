// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using static System.String;

namespace Wpf.Ui.Controls
{
    /// <summary>
    /// Button that opens a URL in a web browser.
    /// </summary>
    public sealed class Hyperlink : Wpf.Ui.Controls.Button
    {
        /// <summary>
        /// DependencyProperty for <see cref="NavigateUri"/>.
        /// </summary>
        public static readonly DependencyProperty NavigateUriProperty = DependencyProperty.Register(
            nameof(NavigateUri),
            typeof(string),
            typeof(Hyperlink),
            new PropertyMetadata(Empty));

        /// <summary>
        /// The URL (or application shortcut) to open.
        /// </summary>
        public string NavigateUri
        {
            get => GetValue(NavigateUriProperty) as string ?? Empty;
            set => SetValue(NavigateUriProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Hyperlink"/> class.
        /// </summary>
        public Hyperlink() => Click += OnClick;

        private void OnClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NavigateUri))
                return;

            if (!Uri.TryCreate(NavigateUri, UriKind.Absolute, out var uri))
                return;

            var processStartInfo = new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(processStartInfo);
        }
    }
}
