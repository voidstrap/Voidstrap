using System;
using System.Globalization;

namespace Hellstrap.Extensions
{
    public static class DateTimeEx
    {
        /// <summary>
        /// Converts the given DateTime to a friendly string representation.
        /// </summary>
        /// <param name="dateTime">The DateTime object to format.</param>
        /// <param name="format">Optional. A custom date and time format string.</param>
        /// <param name="culture">Optional. A CultureInfo object for localization. Defaults to invariant culture.</param>
        /// <returns>A friendly string representation of the DateTime object.</returns>
        public static string ToFriendlyString(this DateTime dateTime, string? format = null, CultureInfo? culture = null)
        {
            try
            {
                culture ??= CultureInfo.InvariantCulture;
                format ??= "dddd, d MMMM yyyy 'at' h:mm:ss tt";

                return dateTime.ToString(format, culture);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid date format specified.", nameof(format), ex);
            }
            catch (CultureNotFoundException ex)
            {
                throw new ArgumentException("Invalid culture specified.", nameof(culture), ex);
            }
        }
    }
}
