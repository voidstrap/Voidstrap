using System;
using System.Globalization;

namespace Voidstrap.Extensions
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a DateTime to a human-friendly string format.
        /// Example: "Monday, 26 May 2025 at 4:30:45 PM"
        /// </summary>
        /// <param name="dateTime">The DateTime to format.</param>
        /// <param name="culture">Optional culture info (defaults to InvariantCulture).</param>
        /// <returns>A formatted, friendly string representation of the date and time.</returns>
        public static string ToFriendlyString(this DateTime dateTime, CultureInfo? culture = null)
        {
            var cultureInfo = culture ?? CultureInfo.InvariantCulture;
            return dateTime.ToString("dddd, d MMMM yyyy 'at' h:mm:ss tt", cultureInfo);
        }
    }
}
