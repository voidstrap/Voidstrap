using System;
using System.Windows.Data;

namespace Wpf.Ui.Converters
{
    /// <summary>
    /// Converts Height to Thickness.
    /// </summary>
    class ProgressThicknessConverter : IValueConverter
    {
        /// <summary>
        /// Converts a height value to a thickness value with flexible scaling and clamping.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double height)
            {
                // Default divisor if no parameter is provided or invalid
                double divisor = 8.0;

                // Try to parse parameter as double if given and positive
                if (parameter != null && double.TryParse(parameter.ToString(), out double paramDivisor) && paramDivisor > 0)
                    divisor = paramDivisor;

                // Calculate thickness
                double thickness = height / divisor;

                // Clamp thickness between min and max values
                const double minThickness = 2.0;
                const double maxThickness = 20.0;

                if (thickness < minThickness)
                    thickness = minThickness;
                else if (thickness > maxThickness)
                    thickness = maxThickness;

                return thickness;
            }

            // fallback default thickness
            return 12.0d;
        }

        /// <summary>
        /// ConvertBack is not implemented.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
