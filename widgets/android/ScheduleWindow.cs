namespace Omnicasa.Schedule.Widget;

/// <summary>An appointment placed within an overlap group: its column and the group's column count.</summary>
public sealed class LaidOutWidgetItem
{
    /// <summary>Initializes a new instance of the <see cref="LaidOutWidgetItem"/> class.</summary>
    /// <param name="appointment">The underlying appointment.</param>
    /// <param name="start">Clamped start time.</param>
    /// <param name="end">Clamped end time.</param>
    /// <param name="column">Zero-based column within the overlap group.</param>
    /// <param name="columnsInGroup">Total columns in the overlap group.</param>
    public LaidOutWidgetItem(WidgetAppointment appointment, DateTime start, DateTime end, int column, int columnsInGroup)
    {
        Appointment = appointment;
        Start = start;
        End = end;
        Column = column;
        ColumnsInGroup = columnsInGroup;
    }

    /// <summary>Gets the underlying appointment.</summary>
    public WidgetAppointment Appointment { get; }

    /// <summary>Gets the start time.</summary>
    public DateTime Start { get; }

    /// <summary>Gets the end time.</summary>
    public DateTime End { get; }

    /// <summary>Gets the zero-based column within the overlap group.</summary>
    public int Column { get; }

    /// <summary>Gets the total number of columns in the overlap group.</summary>
    public int ColumnsInGroup { get; }
}

/// <summary>
/// Pure layout for the schedule widget: picks the dynamic time window around "now" and packs
/// overlapping appointments into columns, mirroring the MAUI <c>ScheduleLayout</c>. No Android
/// drawing here so it can be reasoned about / unit-tested independently of the renderer.
/// </summary>
public sealed class ScheduleWindow
{
    private ScheduleWindow(int startMinutes, int spanMinutes, IReadOnlyList<LaidOutWidgetItem> items, int? nowMinutes)
    {
        StartMinutes = startMinutes;
        SpanMinutes = spanMinutes;
        Items = items;
        NowMinutes = nowMinutes;
    }

    /// <summary>First minute-of-day shown at the top of the grid (0..1440).</summary>
    public int StartMinutes { get; }

    /// <summary>Minutes the window spans (e.g. 6h = 360).</summary>
    public int SpanMinutes { get; }

    /// <summary>Last minute-of-day shown.</summary>
    public int EndMinutes => StartMinutes + SpanMinutes;

    /// <summary>Items intersecting the window, with overlap columns assigned.</summary>
    public IReadOnlyList<LaidOutWidgetItem> Items { get; }

    /// <summary>"now" minute-of-day when the day is in view, else null (no current-time line).</summary>
    public int? NowMinutes { get; }

    /// <summary>Builds the window for a given day.</summary>
    /// <param name="appointments">All shared appointments (filtered to the day + timed items here).</param>
    /// <param name="day">The calendar day to show.</param>
    /// <param name="now">Current time (anchors the window and the now-line).</param>
    /// <param name="windowHours">How many hours the grid spans.</param>
    public static ScheduleWindow Build(IEnumerable<WidgetAppointment> appointments, DateTime day, DateTime now, int windowHours = 6)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        int span = Math.Max(1, windowHours) * 60;

        // Timed appointments intersecting the day (drop all-day; the grid is time-based).
        var dayItems = new List<(WidgetAppointment Appt, int StartM, int EndM)>();
        foreach (var appt in appointments)
        {
            if (appt.Start is not { } s || appt.End is not { } e || e <= s)
            {
                continue;
            }

            if (s >= dayEnd || e <= dayStart)
            {
                continue;
            }

            dayItems.Add((appt, MinuteOfDay(s, dayStart), MinuteOfDay(e, dayStart)));
        }

        bool isToday = now.Date == dayStart;
        int? nowMin = isToday ? MinuteOfDay(now, dayStart) : null;

        // Anchor: one hour of lead-in before "now" (or the first event when not today), floored to
        // the hour and clamped so the window fits inside the day.
        int anchorSource;
        if (nowMin is { } nm)
        {
            anchorSource = nm - 60;
        }
        else if (dayItems.Count > 0)
        {
            anchorSource = dayItems.Min(i => i.StartM) - 30;
        }
        else
        {
            anchorSource = 8 * 60;
        }

        int start = (anchorSource / 60) * 60;
        start = Math.Max(0, Math.Min(start, (24 * 60) - span));
        int windowEnd = start + span;

        var visible = dayItems
            .Where(i => i.StartM < windowEnd && i.EndM > start)
            .OrderBy(i => i.StartM).ThenBy(i => i.EndM)
            .ToList();

        var laid = PackColumns(visible, dayStart);
        return new ScheduleWindow(start, span, laid, nowMin);
    }

    // Greedy overlap-column packing (matches ScheduleLayout in the MAUI control).
    private static IReadOnlyList<LaidOutWidgetItem> PackColumns(
        List<(WidgetAppointment Appt, int StartM, int EndM)> items,
        DateTime dayStart)
    {
        var result = new List<LaidOutWidgetItem>(items.Count);
        var group = new List<(WidgetAppointment Appt, int StartM, int EndM, int Column)>();
        int? groupEnd = null;

        void Flush()
        {
            if (group.Count == 0)
            {
                return;
            }

            int cols = group.Max(g => g.Column) + 1;
            foreach (var g in group)
            {
                result.Add(new LaidOutWidgetItem(g.Appt, dayStart.AddMinutes(g.StartM), dayStart.AddMinutes(g.EndM), g.Column, cols));
            }

            group.Clear();
            groupEnd = null;
        }

        foreach (var (appt, startM, endM) in items)
        {
            if (groupEnd is { } ge && startM >= ge)
            {
                Flush();
            }

            var used = new HashSet<int>();
            foreach (var g in group)
            {
                if (g.EndM > startM)
                {
                    used.Add(g.Column);
                }
            }

            int c = 0;
            while (used.Contains(c))
            {
                c++;
            }

            group.Add((appt, startM, endM, c));
            groupEnd = groupEnd is { } cur ? Math.Max(cur, endM) : endM;
        }

        Flush();
        return result;
    }

    private static int MinuteOfDay(DateTime t, DateTime dayStart) => (int)(t - dayStart).TotalMinutes;
}
