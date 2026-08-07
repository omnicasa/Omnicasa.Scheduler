namespace Omnicasa.Schedule;

/// <summary>
/// How <c>InfiniteScheduleView</c> draws its header while showing a day at a time — one day
/// (<c>ViewMode == 1</c>) or the two-day pair (<c>ViewMode == 2</c>). Ignored in the wider modes.
/// </summary>
public enum ScheduleOneDayHeaderMode
{
    /// <summary>The day band alone (or the day-column header when <c>SingleDayHeader</c> is off).</summary>
    Band,

    /// <summary>
    /// A week's worth of days above the day band, marking the day (or, in two-day mode, the pair) on
    /// screen: the cells hold their places while the marker hands over from the day being left to the
    /// one arriving. Tapping a day glides to it.
    /// </summary>
    WeekStrip,
}
