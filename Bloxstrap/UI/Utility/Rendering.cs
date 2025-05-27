using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Voidstrap.UI.Utility
{
    public static class Rendering
    {
        private static double? _cachedDpi = null;

        public static double GetTextWidth(TextBlock textBlock)
        {
            if (textBlock == null || string.IsNullOrEmpty(textBlock.Text))
                return 0;

            if (_cachedDpi == null)
                _cachedDpi = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;

            var typeface = new Typeface(
                textBlock.FontFamily,
                textBlock.FontStyle,
                textBlock.FontWeight,
                textBlock.FontStretch
            );

            return new FormattedText(
                textBlock.Text,
                CultureInfo.CurrentCulture,
                textBlock.FlowDirection,
                typeface,
                textBlock.FontSize,
                Brushes.Transparent,
                new NumberSubstitution(),
                _cachedDpi.Value
            ).WidthIncludingTrailingWhitespace;
        }
    }
}
