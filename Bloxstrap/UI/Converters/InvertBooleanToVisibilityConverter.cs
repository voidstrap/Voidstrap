using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Voidstrap.UI.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InvertBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
                return flag ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
                return vis != Visibility.Visible;
            return false;
        }
    }
}
