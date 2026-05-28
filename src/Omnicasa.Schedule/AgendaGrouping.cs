using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>One row in <see cref="AgendaListView"/>: either an appointment or a "no events" placeholder.</summary>
public sealed class AgendaEntry
{
    private AgendaEntry(IScheduleItem? item, string title, string timeText, Color accent, bool isPlaceholder)
    {
        Item = item;
        Title = title;
        TimeText = timeText;
        Accent = accent;
        IsPlaceholder = isPlaceholder;
    }

    /// <summary>Gets the underlying appointment, or null for a placeholder row.</summary>
    public IScheduleItem? Item { get; }

    /// <summary>Gets the title to display.</summary>
    public string Title { get; }

    /// <summary>Gets the time range text (empty for placeholders, "All day" for all-day items).</summary>
    public string TimeText { get; }

    /// <summary>Gets the accent color (item color or theme accent; muted for placeholders).</summary>
    public Color Accent { get; }

    /// <summary>Gets a value indicating whether this is a "no events" placeholder.</summary>
    public bool IsPlaceholder { get; }

    /// <summary>Gets a value indicating whether the accent bar should show (false for placeholders).</summary>
    public bool ShowAccent => !IsPlaceholder;

    /// <summary>Creates an entry for a real appointment.</summary>
    /// <param name="item">The appointment.</param>
    /// <param name="theme">Theme used to resolve the fallback accent color.</param>
    /// <returns>The entry.</returns>
    public static AgendaEntry ForItem(IScheduleItem item, ScheduleTheme theme)
    {
        string time = item.IsAllDay
            ? "All day"
            : $"{FormatTime(item.Start)} – {FormatTime(item.End)}";
        return new AgendaEntry(item, item.Title ?? string.Empty, time, item.Color ?? theme.Accent, isPlaceholder: false);
    }

    /// <summary>Creates a "no events" placeholder entry.</summary>
    /// <param name="text">The placeholder text.</param>
    /// <param name="theme">Theme used to resolve the muted color.</param>
    /// <returns>The entry.</returns>
    public static AgendaEntry Placeholder(string text, ScheduleTheme theme)
        => new AgendaEntry(null, text, string.Empty, theme.Muted, isPlaceholder: true);

    private static string FormatTime(DateTime t)
        => t.ToString(t.Minute == 0 ? "h tt" : "h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
}

/// <summary>A single day's group of <see cref="AgendaEntry"/> rows for <see cref="AgendaListView"/>.</summary>
public sealed class AgendaDayGroup : ObservableCollection<AgendaEntry>
{
    /// <summary>Initializes a new instance of the <see cref="AgendaDayGroup"/> class.</summary>
    /// <param name="date">The day this group represents.</param>
    /// <param name="header">The header text (e.g. "Monday, May 25").</param>
    /// <param name="isToday">Whether the day is today.</param>
    /// <param name="headerColor">Resolved header text color.</param>
    public AgendaDayGroup(DateOnly date, string header, bool isToday, Color headerColor)
    {
        Date = date;
        Header = header;
        IsToday = isToday;
        HeaderColor = headerColor;
        WeekdayText = date.ToString("ddd", CultureInfo.CurrentCulture);
        DayNumberText = date.Day.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>Gets the day this group represents.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets the header text (e.g. "Monday, May 25").</summary>
    public string Header { get; }

    /// <summary>Gets the abbreviated weekday for the date column (e.g. "Mon").</summary>
    public string WeekdayText { get; }

    /// <summary>Gets the day-of-month number for the date column (e.g. "25").</summary>
    public string DayNumberText { get; }

    /// <summary>Gets a value indicating whether the day is today.</summary>
    public bool IsToday { get; }

    /// <summary>Gets the resolved header / date-column text color.</summary>
    public Color HeaderColor { get; }
}

/// <summary>
/// A single flat row for <see cref="AgendaListView"/>: one appointment (or placeholder) plus the
/// date metadata for its day. <see cref="ShowDate"/> is true only for the first row of each day, so
/// the date renders once on the left; <see cref="ShowSeparator"/> is true for the last row of a day.
/// Flattening days into rows lets the <see cref="Microsoft.Maui.Controls.CollectionView"/> fully
/// virtualize (no nested repeaters).
/// </summary>
public sealed class AgendaRow
{
    /// <summary>Initializes a new instance of the <see cref="AgendaRow"/> class.</summary>
    /// <param name="entry">The appointment or placeholder shown on the right.</param>
    /// <param name="day">The day this row belongs to (source of the date column values).</param>
    /// <param name="showDate">Whether to render the date column (first row of the day).</param>
    /// <param name="showSeparator">Whether to render a divider after this row (last row of the day).</param>
    public AgendaRow(AgendaEntry entry, AgendaDayGroup day, bool showDate, bool showSeparator)
    {
        Entry = entry;
        Date = day.Date;
        WeekdayText = day.WeekdayText;
        DayNumberText = day.DayNumberText;
        HeaderColor = day.HeaderColor;
        ShowDate = showDate;
        ShowSeparator = showSeparator;
    }

    /// <summary>Gets the appointment / placeholder for this row.</summary>
    public AgendaEntry Entry { get; }

    /// <summary>Gets the day this row belongs to.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets the abbreviated weekday for the date column.</summary>
    public string WeekdayText { get; }

    /// <summary>Gets the day-of-month number for the date column.</summary>
    public string DayNumberText { get; }

    /// <summary>Gets the resolved date-column text color.</summary>
    public Color HeaderColor { get; }

    /// <summary>Gets a value indicating whether the date column should render (first row of the day).</summary>
    public bool ShowDate { get; }

    /// <summary>Gets a value indicating whether a divider should render below this row (last of the day).</summary>
    public bool ShowSeparator { get; }
}

/// <summary>Buckets <see cref="IScheduleItem"/>s into one <see cref="AgendaDayGroup"/> per day in a range.</summary>
public static class AgendaGrouping
{
    /// <summary>Builds the flat agenda rows for a day range (one row per appointment / empty-day placeholder).</summary>
    /// <param name="items">All candidate items.</param>
    /// <param name="from">Inclusive first day.</param>
    /// <param name="to">Inclusive last day.</param>
    /// <param name="emptyText">Placeholder text for days with no items.</param>
    /// <param name="theme">Theme used to resolve colors.</param>
    /// <returns>Flat rows in ascending date order.</returns>
    public static IReadOnlyList<AgendaRow> BuildRows(
        IEnumerable<IScheduleItem> items,
        DateOnly from,
        DateOnly to,
        string emptyText,
        ScheduleTheme theme)
    {
        var rows = new List<AgendaRow>();
        foreach (var day in Build(items, from, to, emptyText, theme))
        {
            for (int i = 0; i < day.Count; i++)
            {
                rows.Add(new AgendaRow(day[i], day, showDate: i == 0, showSeparator: i == day.Count - 1));
            }
        }

        return rows;
    }

    /// <summary>
    /// Builds one group per day in <c>[from, to]</c> (inclusive). Each day lists the items that
    /// intersect it (multi-day items appear on every day they span), sorted by start then end;
    /// empty days get a single placeholder row.
    /// </summary>
    /// <param name="items">All candidate items (only those intersecting the range are used).</param>
    /// <param name="from">Inclusive first day.</param>
    /// <param name="to">Inclusive last day.</param>
    /// <param name="emptyText">Placeholder text for days with no items.</param>
    /// <param name="theme">Theme used to resolve colors.</param>
    /// <returns>One group per day, in ascending date order.</returns>
    public static IReadOnlyList<AgendaDayGroup> Build(
        IEnumerable<IScheduleItem> items,
        DateOnly from,
        DateOnly to,
        string emptyText,
        ScheduleTheme theme)
    {
        var byDay = new Dictionary<DateOnly, List<IScheduleItem>>();
        foreach (var item in items)
        {
            var d0 = DateOnly.FromDateTime(item.Start);
            var d1 = DateOnly.FromDateTime(item.End);
            if (d1 < d0)
            {
                d1 = d0;
            }

            if (d1 < from || d0 > to)
            {
                continue;
            }

            var start = d0 < from ? from : d0;
            var end = d1 > to ? to : d1;
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (!byDay.TryGetValue(d, out var list))
                {
                    list = new List<IScheduleItem>();
                    byDay[d] = list;
                }

                list.Add(item);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<AgendaDayGroup>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            bool isToday = d == today;
            var header = HeaderFor(d, isToday);
            var headerColor = isToday ? theme.Today : theme.Foreground;
            var group = new AgendaDayGroup(d, header, isToday, headerColor);

            if (byDay.TryGetValue(d, out var dayItems))
            {
                foreach (var item in dayItems.OrderBy(a => a.Start).ThenBy(a => a.End))
                {
                    group.Add(AgendaEntry.ForItem(item, theme));
                }
            }
            else
            {
                group.Add(AgendaEntry.Placeholder(emptyText, theme));
            }

            result.Add(group);
        }

        return result;
    }

    private static string HeaderFor(DateOnly date, bool isToday)
    {
        var label = date.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
        return isToday ? $"Today · {label}" : label;
    }
}
