using System.Globalization;

namespace Omnicasa.Schedule;

/// <summary>Formats the time-rail hour labels, honouring an optional custom format string.</summary>
internal static class HourLabelFormatter
{
    /// <summary>
    /// Formats <paramref name="hour"/> (0–23) for the time rail. A null or empty
    /// <paramref name="format"/> keeps the default 12-hour style ("12 AM" … "11 PM");
    /// otherwise it is a standard .NET date-time format string applied to the hour,
    /// e.g. "H" → "23", "HH:mm" → "23:00", "h tt" → "11 PM".
    /// </summary>
    public static string Format(int hour, string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return hour switch
            {
                0 => "12 AM",
                12 => "12 PM",
                < 12 => $"{hour} AM",
                _ => $"{hour - 12} PM",
            };
        }

        return Custom(new DateTime(2000, 1, 1).AddHours(hour), format!);
    }

    /// <summary>Formats an arbitrary time with a custom format string, e.g. "HH:mm" → "09:59".</summary>
    public static string Custom(DateTime time, string format)
    {
        // A single character would be read as a standard format specifier; "%" forces custom ("H" → "%H").
        var custom = format.Length == 1 ? "%" + format : format;
        return time.ToString(custom, CultureInfo.CurrentCulture);
    }
}
