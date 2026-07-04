using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleColumnBuilderTests
{
    // A Monday.
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static TestScheduleItem Item(int day, int startHour, int endHour, string? personId = null) =>
        new TestScheduleItem
        {
            Id = $"{day}:{startHour}-{endHour}",
            Start = Day.AddDays(day).AddHours(startHour),
            End = Day.AddDays(day).AddHours(endHour),
            PersonId = personId,
        };

    [Fact]
    public void EffectiveDays_ClampsViewModeToRange()
    {
        Assert.Equal(3, ScheduleColumnBuilder.EffectiveDays(Day, Day.AddDays(2), 7));
        Assert.Equal(2, ScheduleColumnBuilder.EffectiveDays(Day, Day.AddDays(6), 2));
    }

    [Fact]
    public void EffectiveDays_EndBeforeStart_IsOneDay()
    {
        Assert.Equal(1, ScheduleColumnBuilder.EffectiveDays(Day, Day.AddDays(-3), 7));
    }

    [Fact]
    public void Build_SingleDayNoPersons_KeepsDayNumberForDetachedHeaders()
    {
        var cols = ScheduleColumnBuilder.Build(Day, Day, 1, persons: null);

        var col = Assert.Single(cols);
        Assert.Equal("MON", col.HeaderPrimary);
        Assert.Equal("25", col.HeaderSecondary);
        Assert.Null(col.PersonId);
        Assert.Empty(col.Items);
    }

    [Fact]
    public void Build_WeekNoPersons_UsesDayNumberAsSecondary()
    {
        var cols = ScheduleColumnBuilder.Build(Day, Day.AddDays(6), 7, persons: null);

        Assert.Equal(7, cols.Length);
        Assert.Equal("MON", cols[0].HeaderPrimary);
        Assert.Equal("25", cols[0].HeaderSecondary);
        Assert.Equal("SUN", cols[6].HeaderPrimary);
        Assert.Equal("31", cols[6].HeaderSecondary);
    }

    [Fact]
    public void Build_PersonsMode_SplitsEachDayPerPerson()
    {
        var persons = new List<IPerson>
        {
            new Person { Id = "a", Name = "Ann Lee", Color = Colors.Red },
            new Person { Id = "b", Name = "Bob" },
        };

        var cols = ScheduleColumnBuilder.Build(Day, Day.AddDays(1), 2, persons);

        Assert.Equal(4, cols.Length);
        Assert.Equal("MON 25", cols[0].HeaderPrimary);
        Assert.Equal("AL", cols[0].HeaderSecondary);
        Assert.Equal("a", cols[0].PersonId);
        Assert.Equal(Colors.Red, cols[0].Accent);
        Assert.Equal("BO", cols[1].HeaderSecondary);
        Assert.Equal("TUE 26", cols[2].HeaderPrimary);
    }

    [Fact]
    public void Build_MarksInjectedToday()
    {
        var cols = ScheduleColumnBuilder.Build(
            Day,
            Day.AddDays(2),
            3,
            persons: null,
            today: DateOnly.FromDateTime(Day.AddDays(1)));

        Assert.False(cols[0].IsToday);
        Assert.True(cols[1].IsToday);
        Assert.False(cols[2].IsToday);
    }

    [Fact]
    public void Build_PlacesItemsInTheirDayAndPersonColumns()
    {
        var persons = new List<IPerson>
        {
            new Person { Id = "a", Name = "Ann" },
            new Person { Id = "b", Name = "Bob" },
        };
        var items = new IScheduleItem[]
        {
            Item(0, 9, 10, personId: "a"),
            Item(1, 11, 12, personId: "b"),
        };

        var cols = ScheduleColumnBuilder.Build(Day, Day.AddDays(1), 2, persons, items);

        Assert.Single(cols[0].Items);
        Assert.Empty(cols[1].Items);
        Assert.Empty(cols[2].Items);
        Assert.Single(cols[3].Items);
    }

    [Fact]
    public void Build_ExcludesSpanningItemsFromColumns()
    {
        var items = new IScheduleItem[]
        {
            new TestScheduleItem { Id = "allday", Start = Day, End = Day.AddDays(1), IsAllDay = true },
            Item(0, 9, 10),
        };

        var cols = ScheduleColumnBuilder.Build(Day, Day, 1, persons: null, items);

        var laid = Assert.Single(Assert.Single(cols).Items);
        Assert.Equal("0:9-10", laid.Item.Id);
    }

    [Theory]
    [InlineData("Ann Lee", "AL")]
    [InlineData("Bob", "BO")]
    [InlineData("X", "X")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    public void Initials_FollowNameShape(string? name, string expected)
    {
        Assert.Equal(expected, ScheduleColumnBuilder.Initials(name));
    }
}
