namespace Omnicasa.Schedule;

/// <summary>
/// How <c>InfiniteScheduleView</c> draws its header while showing a single day
/// (<c>ViewMode == 1</c>). Ignored in the multi-day modes.
/// </summary>
public enum ScheduleOneDayHeaderMode
{
    /// <summary>The day band alone (or the day-column header when <c>SingleDayHeader</c> is off).</summary>
    Band,

    /// <summary>
    /// A week's worth of days above the day band, centred on the current one: the days ride the day
    /// axis and swell into a bubble fixed in the middle as the swipe carries them through it.
    /// Tapping a day glides to it.
    /// </summary>
    WeekStrip,
}
