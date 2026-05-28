namespace Omnicasa.Schedule;

/// <summary>
/// An all-day / multi-day appointment positioned in the all-day panel above <see cref="ScheduleView"/>'s
/// time grid: which day columns it spans (inclusive, clamped to the visible range) and its stacking lane.
/// </summary>
public sealed class AllDayBar
{
    /// <summary>Initializes a new instance of the <see cref="AllDayBar"/> class.</summary>
    /// <param name="item">The underlying item.</param>
    /// <param name="startDay">Inclusive first visible day column index it covers.</param>
    /// <param name="endDay">Inclusive last visible day column index it covers.</param>
    /// <param name="lane">Zero-based stacking lane (row) within the panel.</param>
    public AllDayBar(IScheduleItem item, int startDay, int endDay, int lane)
    {
        Item = item;
        StartDay = startDay;
        EndDay = endDay;
        Lane = lane;
    }

    /// <summary>Gets the underlying item.</summary>
    public IScheduleItem Item { get; }

    /// <summary>Gets the inclusive first day-column index the bar covers.</summary>
    public int StartDay { get; }

    /// <summary>Gets the inclusive last day-column index the bar covers.</summary>
    public int EndDay { get; }

    /// <summary>Gets the zero-based stacking lane.</summary>
    public int Lane { get; }
}

/// <summary>
/// Lays out all-day and cross-date (multi-day) items into horizontal bars stacked in lanes, for the
/// panel shown above <see cref="ScheduleView"/>'s time grid. Pure and stateless.
/// </summary>
public static class AllDayLayout
{
    /// <summary>
    /// True when an item belongs in the all-day panel rather than the time grid: it is marked
    /// all-day, or it spans more than one calendar day.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns>True for all-day or cross-date items.</returns>
    public static bool IsSpanning(IScheduleItem item)
        => item.IsAllDay || DateOnly.FromDateTime(item.Start) != EndDateOf(item);

    /// <summary>
    /// Packs the spanning items into lanes across the visible day range <c>[rangeStart, rangeStart + days)</c>.
    /// Each item is clamped to the range; items entirely outside it are dropped. Lanes are assigned
    /// greedily so non-overlapping bars share a lane.
    /// </summary>
    /// <param name="items">Candidate items (typically those for which <see cref="IsSpanning"/> is true).</param>
    /// <param name="rangeStart">First visible day (column 0).</param>
    /// <param name="days">Number of visible day columns.</param>
    /// <returns>The laid-out bars in lane order; empty when nothing is visible.</returns>
    public static IReadOnlyList<AllDayBar> Layout(IEnumerable<IScheduleItem> items, DateOnly rangeStart, int days)
    {
        if (days <= 0)
        {
            return Array.Empty<AllDayBar>();
        }

        int lastDay = days - 1;
        var visible = new List<(IScheduleItem Item, int Start, int End)>();
        foreach (var item in items)
        {
            int start = DateOnly.FromDateTime(item.Start).DayNumber - rangeStart.DayNumber;
            int end = EndDateOf(item).DayNumber - rangeStart.DayNumber;
            if (end < 0 || start > lastDay)
            {
                continue;
            }

            visible.Add((item, Math.Max(0, start), Math.Min(lastDay, end)));
        }

        visible.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));

        var bars = new List<AllDayBar>(visible.Count);
        var laneEnds = new List<int>();
        foreach (var (item, start, end) in visible)
        {
            int lane = -1;
            for (int l = 0; l < laneEnds.Count; l++)
            {
                if (laneEnds[l] < start)
                {
                    lane = l;
                    break;
                }
            }

            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(end);
            }
            else
            {
                laneEnds[lane] = end;
            }

            bars.Add(new AllDayBar(item, start, end, lane));
        }

        return bars;
    }

    // The last calendar day an item occupies. An event ending exactly at midnight is treated as
    // ending the previous day (so [day1 00:00, day2 00:00) spans only day1).
    private static DateOnly EndDateOf(IScheduleItem item)
    {
        var endDate = DateOnly.FromDateTime(item.End);
        if (item.End.TimeOfDay == TimeSpan.Zero && item.End > item.Start)
        {
            endDate = endDate.AddDays(-1);
        }

        return endDate;
    }
}
