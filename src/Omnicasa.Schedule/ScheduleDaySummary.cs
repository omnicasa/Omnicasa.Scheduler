using System.Globalization;

namespace Omnicasa.Schedule;

/// <summary>
/// What the one-day header knows about the day it shows. Handed to
/// <c>InfiniteScheduleView.DaySummaryFormatter</c> so an app can supply its own (localized) line
/// in place of the built-in English text.
/// </summary>
public readonly struct ScheduleDaySummary
{
    /// <summary>Gets the day the header is showing.</summary>
    public DateTime Day { get; init; }

    /// <summary>Gets a value indicating whether <see cref="Day"/> is today.</summary>
    public bool IsToday { get; init; }

    /// <summary>Gets the number of timed appointments on the day (all-day/multi-day items excluded).</summary>
    public int AppointmentCount { get; init; }

    /// <summary>Gets the number of all-day / multi-day items on the day (the ones drawn as bars).</summary>
    public int AllDayCount { get; init; }

    /// <summary>
    /// Built-in text used when no formatter is set: "Nothing planned", "1 appointment", or
    /// "{n} appointments".
    /// </summary>
    /// <param name="summary">The day's counts.</param>
    /// <returns>A short English summary line.</returns>
    public static string DefaultText(ScheduleDaySummary summary)
        => summary.AppointmentCount switch
        {
            <= 0 => "Nothing planned",
            1 => "1 appointment",
            _ => string.Format(CultureInfo.CurrentCulture, "{0} appointments", summary.AppointmentCount),
        };
}
