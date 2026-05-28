using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class AgendaGroupingTests
{
    private static readonly ScheduleTheme Theme = new ScheduleTheme();

    private static readonly DateOnly From = new DateOnly(2026, 5, 25);
    private static readonly DateOnly To = new DateOnly(2026, 5, 29);

    private static TestScheduleItem Item(DateOnly day, int startHour, int endHour, string id) =>
        new TestScheduleItem
        {
            Id = id,
            Start = day.ToDateTime(new TimeOnly(startHour, 0)),
            End = day.ToDateTime(new TimeOnly(endHour, 0)),
        };

    [Fact]
    public void Build_EmitsOneGroupPerDayInRange()
    {
        var groups = AgendaGrouping.Build(Array.Empty<IScheduleItem>(), From, To, "No events", Theme);

        Assert.Equal(5, groups.Count);                          // 25..29 inclusive
        Assert.Equal(From, groups[0].Date);
        Assert.Equal(To, groups[^1].Date);
    }

    [Fact]
    public void Build_EmptyDay_HasSinglePlaceholderEntry()
    {
        var groups = AgendaGrouping.Build(Array.Empty<IScheduleItem>(), From, To, "Nothing", Theme);

        var first = groups[0];
        var entry = Assert.Single(first);
        Assert.True(entry.IsPlaceholder);
        Assert.Equal("Nothing", entry.Title);
        Assert.False(entry.ShowAccent);
    }

    [Fact]
    public void Build_PlacesItemOnItsDay_SortedByStart()
    {
        var d = new DateOnly(2026, 5, 26);
        var items = new IScheduleItem[]
        {
            Item(d, 14, 15, "afternoon"),
            Item(d, 9, 10, "morning"),
        };

        var groups = AgendaGrouping.Build(items, From, To, "No events", Theme);
        var day26 = groups.Single(g => g.Date == d);

        Assert.Equal(2, day26.Count);
        Assert.Equal("morning", day26[0].Item!.Id);
        Assert.Equal("afternoon", day26[1].Item!.Id);
        Assert.All(day26, e => Assert.False(e.IsPlaceholder));
    }

    [Fact]
    public void Build_MultiDayItem_AppearsOnEveryDaySpanned()
    {
        var trip = new TestScheduleItem
        {
            Id = "trip",
            Start = new DateOnly(2026, 5, 26).ToDateTime(new TimeOnly(9, 0)),
            End = new DateOnly(2026, 5, 28).ToDateTime(new TimeOnly(17, 0)),
        };

        var groups = AgendaGrouping.Build(new IScheduleItem[] { trip }, From, To, "No events", Theme);

        Assert.Contains(groups.Single(g => g.Date == new DateOnly(2026, 5, 26)), g => g.Item?.Id == "trip");
        Assert.Contains(groups.Single(g => g.Date == new DateOnly(2026, 5, 27)), g => g.Item?.Id == "trip");
        Assert.Contains(groups.Single(g => g.Date == new DateOnly(2026, 5, 28)), g => g.Item?.Id == "trip");
        // Days outside the span are placeholders.
        Assert.True(groups.Single(g => g.Date == new DateOnly(2026, 5, 25))[0].IsPlaceholder);
    }

    [Fact]
    public void Build_IgnoresItemsOutsideRange()
    {
        var outside = Item(new DateOnly(2026, 1, 1), 9, 10, "old");

        var groups = AgendaGrouping.Build(new IScheduleItem[] { outside }, From, To, "No events", Theme);

        Assert.All(groups, g => Assert.True(g[0].IsPlaceholder));
    }

    [Fact]
    public void BuildRows_ShowsDateOnFirstRowAndSeparatorOnLastRowOfDay()
    {
        var d = new DateOnly(2026, 5, 26);
        var items = new IScheduleItem[] { Item(d, 9, 10, "a"), Item(d, 11, 12, "b") };

        var rows = AgendaGrouping.BuildRows(items, d, d, "No events", Theme);

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].ShowDate);
        Assert.False(rows[0].ShowSeparator);
        Assert.False(rows[1].ShowDate);            // date shown once, on the day's first row
        Assert.True(rows[1].ShowSeparator);        // divider after the day's last row
    }

    [Fact]
    public void BuildRows_EmptyDay_IsOneRowWithDateAndPlaceholder()
    {
        var rows = AgendaGrouping.BuildRows(Array.Empty<IScheduleItem>(), From, From, "No events", Theme);

        var row = Assert.Single(rows);
        Assert.True(row.ShowDate);
        Assert.True(row.ShowSeparator);
        Assert.True(row.Entry.IsPlaceholder);
    }

    [Fact]
    public void BuildRows_RowCarriesDayDateMetadataForTheDateColumn()
    {
        var d = new DateOnly(2026, 5, 26);
        var rows = AgendaGrouping.BuildRows(new IScheduleItem[] { Item(d, 9, 10, "a") }, d, d, "No events", Theme);

        var row = Assert.Single(rows);
        Assert.Equal(d, row.Date);
        Assert.Equal("26", row.DayNumberText);
        Assert.False(string.IsNullOrEmpty(row.WeekdayText));
        Assert.Equal("a", row.Entry.Item!.Id);   // ItemTemplate binds AgendaRow.Entry (an AgendaEntry)
    }

    [Fact]
    public void AgendaEntry_ForItem_UsesItemColorAsAccent_ForCustomTemplates()
    {
        var d = new DateOnly(2026, 5, 26);
        var colored = new TestScheduleItem
        {
            Id = "c",
            Start = d.ToDateTime(new TimeOnly(9, 0)),
            End = d.ToDateTime(new TimeOnly(10, 0)),
            Color = Colors.Red,
        };

        var entry = AgendaGrouping.BuildRows(new IScheduleItem[] { colored }, d, d, "No events", Theme)[0].Entry;

        Assert.True(entry.ShowAccent);
        Assert.Equal(Colors.Red, entry.Accent);
    }

    [Fact]
    public void Build_AllDayItem_ShowsAllDayText()
    {
        var allDay = new TestScheduleItem
        {
            Id = "holiday",
            IsAllDay = true,
            Start = new DateOnly(2026, 5, 27).ToDateTime(TimeOnly.MinValue),
            End = new DateOnly(2026, 5, 27).ToDateTime(TimeOnly.MaxValue),
        };

        var groups = AgendaGrouping.Build(new IScheduleItem[] { allDay }, From, To, "No events", Theme);
        var entry = groups.Single(g => g.Date == new DateOnly(2026, 5, 27)).Single(e => !e.IsPlaceholder);

        Assert.Equal("All day", entry.TimeText);
    }
}
