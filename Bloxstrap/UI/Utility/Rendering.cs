using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Voidstrap.UI.Utility
{
    public static class Rendering
    {
        private static double? _cachedDpi;
        public static double GetTextWidth(TextBlock textBlock)
        {
            if (textBlock is null)
                return 0;

            string text = textBlock.Text;
            if (string.IsNullOrEmpty(text))
                return 0;

            _cachedDpi ??= VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;
            TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);

            var typeface = new Typeface(
                textBlock.FontFamily,
                textBlock.FontStyle,
                textBlock.FontWeight,
                textBlock.FontStretch
            );

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                textBlock.FlowDirection,
                typeface,
                textBlock.FontSize,
                Brushes.Transparent,
                _cachedDpi.Value
            )
            {
                TextAlignment = TextAlignment.Left,
                Trimming = TextTrimming.None
            };
            return formattedText.WidthIncludingTrailingWhitespace;
        }
    }
}
