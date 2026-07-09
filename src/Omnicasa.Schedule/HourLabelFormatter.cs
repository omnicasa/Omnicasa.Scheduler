using System.Globalization;

namespace Omnicasa.Schedule;

/// <summary>Formats the time-rail hour labels, honouring an optional custom format string.</summary>
internal static class HourLabelFormatter
{
    /// <summary>
    /// Formats <paramref name="hour"/> (0–24) for the time rail. A null or empty
    /// <paramref name="format"/> keeps the default 12-hour style ("12 AM" … "11 PM");
    /// otherwise it is a standard .NET date-time format string applied to the hour,
    /// e.g. "H" → "23", "HH:mm" → "23:00", "h tt" → "11 PM". Hour 24 (end of day)
    /// renders as "24" in 24-hour formats, e.g. "HH:mm" → "24:00".
    /// </summary>
    public static string Format(int hour, string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return hour switch
            {
                0 or 24 => "12 AM",
                12 => "12 PM",
                < 12 => $"{hour} AM",
                _ => $"{hour - 12} PM",
            };
        }

        if (hour == 24 && format!.Contains('H'))
        {
            // DateTime can't represent 24:00 — swap the 24-hour token for a literal.
            var literal = System.Text.RegularExpressions.Regex.Replace(format, "H+", "\"24\"");
            return Custom(new DateTime(2000, 1, 1), literal);
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
