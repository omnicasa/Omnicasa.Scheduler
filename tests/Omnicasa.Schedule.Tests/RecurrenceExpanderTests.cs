using System.Linq;
using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class RecurrenceExpanderTests
{
    private static TestScheduleItem Template(DateTime start, TimeSpan? duration = null) => new()
    {
        Id = "tpl",
        Title = "Standup",
        Start = start,
        End = start + (duration ?? TimeSpan.FromHours(1)),
        Color = Colors.DodgerBlue,
        PersonId = "p1",
        Notes = "n",
    };

    [Fact]
    public void Daily_EveryTwoDays_StopsAtCount()
    {
        var template = Template(new DateTime(2026, 1, 1, 9, 0, 0));
        var rule = new RecurrenceRule { Frequency = RecurrenceFrequency.Daily, Interval = 2, Count = 5 };

        var occ = RecurrenceExpander.Expand(template, rule, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Equal(5, occ.Count);
        Assert.Equal(
            new[] { 1, 3, 5, 7, 9 },
            occ.Select(o => o.Start.Day).ToArray());
        Assert.All(occ, o => Assert.Equal(TimeSpan.FromHours(9), o.Start.TimeOfDay));
        Assert.All(occ, o => Assert.Equal(TimeSpan.FromHours(1), o.End - o.Start));
    }

    [Fact]
    public void Weekly_MonWedFri_WithinTwoWeekWindow()
    {
        // 2026-01-05 is a Monday.
        var template = Template(new DateTime(2026, 1, 5, 9, 0, 0));
        var rule = new RecurrenceRule
        {
            Frequency = RecurrenceFrequency.Weekly,
            ByWeekday = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
        };

        var occ = RecurrenceExpander.Expand(template, rule, new DateTime(2026, 1, 5), new DateTime(2026, 1, 19));

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 1, 5, 9, 0, 0),
                new DateTime(2026, 1, 7, 9, 0, 0),
                new DateTime(2026, 1, 9, 9, 0, 0),
                new DateTime(2026, 1, 12, 9, 0, 0),
                new DateTime(2026, 1, 14, 9, 0, 0),
                new DateTime(2026, 1, 16, 9, 0, 0),
            },
            occ.Select(o => o.Start).ToArray());
    }

    [Fact]
    public void Monthly_OnThe31st_SkipsShortMonths()
    {
        var template = Template(new DateTime(2026, 1, 31, 9, 0, 0));
        var rule = new RecurrenceRule { Frequency = RecurrenceFrequency.Monthly };

        var occ = RecurrenceExpander.Expand(
            template,
            rule,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31, 23, 59, 59));

        // Only months with 31 days: Jan, Mar, May, Jul, Aug, Oct, Dec.
        Assert.Equal(new[] { 1, 3, 5, 7, 8, 10, 12 }, occ.Select(o => o.Start.Month).ToArray());
        Assert.All(occ, o => Assert.Equal(31, o.Start.Day));
    }

    [Fact]
    public void Until_CutsOffOccurrences()
    {
        var template = Template(new DateTime(2026, 1, 1, 9, 0, 0));
        var rule = new RecurrenceRule
        {
            Frequency = RecurrenceFrequency.Daily,
            Until = new DateTime(2026, 1, 5),
        };

        var occ = RecurrenceExpander.Expand(template, rule, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Equal(5, occ.Count);
        Assert.Equal(new DateTime(2026, 1, 5, 9, 0, 0), occ[^1].Start);
    }

    [Fact]
    public void Window_ClipsAndKeepsStableIndex()
    {
        var template = Template(new DateTime(2026, 1, 1, 9, 0, 0));
        var rule = new RecurrenceRule { Frequency = RecurrenceFrequency.Daily };

        var occ = RecurrenceExpander.Expand(template, rule, new DateTime(2026, 1, 10), new DateTime(2026, 1, 15));

        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, occ.Select(o => o.Start.Day).ToArray());

        // Ids carry the occurrence index counted from the template start (Jan 1 = 0 → Jan 10 = 9).
        Assert.Equal("tpl#9", occ[0].Id);
        Assert.Equal("tpl#13", occ[^1].Id);
    }

    [Fact]
    public void Expand_DoesNotMutateTemplate()
    {
        var start = new DateTime(2026, 1, 1, 9, 0, 0);
        var template = Template(start);
        var rule = new RecurrenceRule { Frequency = RecurrenceFrequency.Daily, Count = 3 };

        RecurrenceExpander.Expand(template, rule, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Equal(start, template.Start);
        Assert.Equal(start + TimeSpan.FromHours(1), template.End);
        Assert.Equal("tpl", template.Id);
    }
}
