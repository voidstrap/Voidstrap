using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;

            return false;
        }
    }
}

// I am hiding this message here everything you known about me is a lie I am gothic and emo and very depressed person people may say I need help ik ik dw I understand that if you see this message I quit voidstrap most likely because I open sourced the code but I promise I am prob fine idk just stay away from me and yeah sorry for everything!