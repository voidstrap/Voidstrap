using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Voidstrap.UI.Converters
{
    public class TagColorConverter : IValueConverter
    {
        private static Color Darken(Color color, double factor = 0.7)
        {
            return Color.FromRgb(
                (byte)(color.R * factor),
                (byte)(color.G * factor),
                (byte)(color.B * factor)
            );
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "Performance" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                "LOD" => new SolidColorBrush(Darken(Color.FromRgb(41, 122, 175))),
                "Fix" => new SolidColorBrush(Darken(Color.FromRgb(231, 76, 60))),
                "Graphics" => new SolidColorBrush(Darken(Color.FromRgb(26, 188, 156))),
                "Experimental" => new SolidColorBrush(Darken(Color.FromRgb(241, 196, 15))),
                "UI" => new SolidColorBrush(Darken(Color.FromRgb(155, 89, 182))),
                "Unknown" => new SolidColorBrush(Darken(Color.FromRgb(149, 165, 166))),
                _ when value.ToString().StartsWith("+") => new SolidColorBrush(Darken(Color.FromRgb(127, 140, 141))),
                _ => new SolidColorBrush(Darken(Colors.Gray))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
