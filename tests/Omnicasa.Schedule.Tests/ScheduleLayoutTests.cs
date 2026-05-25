using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleLayoutTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static TestScheduleItem Item(int startHour, int endHour, bool allDay = false, string? id = null) =>
        new TestScheduleItem
        {
            Id = id ?? $"{startHour}-{endHour}",
            Start = Day.AddHours(startHour),
            End = Day.AddHours(endHour),
            IsAllDay = allDay,
        };

    [Fact]
    public void Layout_EmptyInput_ReturnsEmpty()
    {
        var result = ScheduleLayout.Layout(Array.Empty<IScheduleItem>());
        Assert.Empty(result);
    }

    [Fact]
    public void Layout_ExcludesAllDayItems()
    {
        var items = new IScheduleItem[] { Item(9, 10), Item(0, 24, allDay: true) };

        var result = ScheduleLayout.Layout(items);

        Assert.Single(result);
        Assert.Equal("9-10", result[0].Item.Id);
    }

    [Fact]
    public void Layout_NonOverlappingItems_EachGetsSingleColumn()
    {
        var items = new IScheduleItem[] { Item(9, 10), Item(11, 12) };

        var result = ScheduleLayout.Layout(items);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(0, r.Column));
        Assert.All(result, r => Assert.Equal(1, r.ColumnsInGroup));
    }

    [Fact]
    public void Layout_TwoOverlappingItems_GetSeparateColumnsInGroupOfTwo()
    {
        var a = Item(9, 10, id: "a");        // 09:00–10:00
        var b = new TestScheduleItem       // 09:30–10:30 (overlaps a)
        {
            Id = "b",
            Start = Day.AddHours(9).AddMinutes(30),
            End = Day.AddHours(10).AddMinutes(30),
        };

        var result = ScheduleLayout.Layout(new IScheduleItem[] { a, b });

        var ra = result.Single(r => r.Item.Id == "a");
        var rb = result.Single(r => r.Item.Id == "b");
        Assert.Equal(0, ra.Column);
        Assert.Equal(1, rb.Column);
        Assert.Equal(2, ra.ColumnsInGroup);
        Assert.Equal(2, rb.ColumnsInGroup);
    }

    [Fact]
    public void Layout_ThreeMutuallyOverlapping_ProduceThreeColumns()
    {
        var a = new TestScheduleItem { Id = "a", Start = Day.AddHours(9), End = Day.AddHours(12) };
        var b = new TestScheduleItem { Id = "b", Start = Day.AddHours(9).AddMinutes(30), End = Day.AddHours(10).AddMinutes(30) };
        var c = new TestScheduleItem { Id = "c", Start = Day.AddHours(10), End = Day.AddHours(11) };

        var result = ScheduleLayout.Layout(new IScheduleItem[] { a, b, c });

        Assert.All(result, r => Assert.Equal(3, r.ColumnsInGroup));
        Assert.Equal(new[] { 0, 1, 2 }, result.OrderBy(r => r.Column).Select(r => r.Column));
    }

    [Fact]
    public void Layout_AdjacentTouchingItems_DoNotOverlap()
    {
        // a ends exactly when b starts -> not overlapping -> both column 0, separate groups.
        var a = Item(9, 10, id: "a");
        var b = Item(10, 11, id: "b");

        var result = ScheduleLayout.Layout(new IScheduleItem[] { a, b });

        Assert.All(result, r => Assert.Equal(0, r.Column));
        Assert.All(result, r => Assert.Equal(1, r.ColumnsInGroup));
    }

    [Fact]
    public void Layout_UnsortedInput_IsSortedByStart()
    {
        var result = ScheduleLayout.Layout(new IScheduleItem[] { Item(14, 15, id: "late"), Item(8, 9, id: "early") });

        Assert.Equal("early", result[0].Item.Id);
        Assert.Equal("late", result[1].Item.Id);
    }

    [Fact]
    public void Layout_OnlyAllDayItems_ReturnsEmpty()
    {
        var result = ScheduleLayout.Layout(new IScheduleItem[] { Item(0, 24, allDay: true), Item(9, 10, allDay: true) });

        Assert.Empty(result);
    }

    [Fact]
    public void Layout_ContainedItems_ReuseFreedColumn()
    {
        // A spans the whole window; B and C are short, sequential, and both nest inside A but
        // don't overlap each other — so C should reuse the column B vacated. Group width stays 2.
        var a = new TestScheduleItem { Id = "a", Start = Day.AddHours(9), End = Day.AddHours(17) };
        var b = new TestScheduleItem { Id = "b", Start = Day.AddHours(10), End = Day.AddHours(11) };
        var c = new TestScheduleItem { Id = "c", Start = Day.AddHours(12), End = Day.AddHours(13) };

        var result = ScheduleLayout.Layout(new IScheduleItem[] { a, b, c });

        Assert.All(result, r => Assert.Equal(2, r.ColumnsInGroup));
        Assert.Equal(0, result.Single(r => r.Item.Id == "a").Column);
        Assert.Equal(1, result.Single(r => r.Item.Id == "b").Column);
        Assert.Equal(1, result.Single(r => r.Item.Id == "c").Column);
    }

    [Fact]
    public void Layout_SameStartDifferentEnd_OrdersShorterFirst()
    {
        var longer = new TestScheduleItem { Id = "long", Start = Day.AddHours(9), End = Day.AddHours(11) };
        var shorter = new TestScheduleItem { Id = "short", Start = Day.AddHours(9), End = Day.AddHours(10) };

        var result = ScheduleLayout.Layout(new IScheduleItem[] { longer, shorter });

        // Sorted by start, then end: shorter comes first and takes column 0.
        Assert.Equal("short", result[0].Item.Id);
        Assert.Equal(0, result[0].Column);
        Assert.All(result, r => Assert.Equal(2, r.ColumnsInGroup));
    }

    [Fact]
    public void Layout_SeparateOverlapGroups_AreIndependent()
    {
        // Group 1: two overlapping in the morning. Group 2: one in the afternoon.
        var a = new TestScheduleItem { Id = "a", Start = Day.AddHours(9), End = Day.AddHours(10).AddMinutes(30) };
        var b = new TestScheduleItem { Id = "b", Start = Day.AddHours(10), End = Day.AddHours(11) };
        var c = new TestScheduleItem { Id = "c", Start = Day.AddHours(15), End = Day.AddHours(16) };

        var result = ScheduleLayout.Layout(new IScheduleItem[] { a, b, c });

        Assert.Equal(2, result.Single(r => r.Item.Id == "a").ColumnsInGroup);
        Assert.Equal(2, result.Single(r => r.Item.Id == "b").ColumnsInGroup);
        Assert.Equal(1, result.Single(r => r.Item.Id == "c").ColumnsInGroup);
    }

    [Fact]
    public void Layout_PreservesEveryNonAllDayItem()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => (IScheduleItem)Item(i, i + 1, id: $"i{i}"))
            .ToArray();

        var result = ScheduleLayout.Layout(items);

        Assert.Equal(10, result.Count);
        Assert.Equal(items.Select(i => i.Id).OrderBy(x => x), result.Select(r => r.Item.Id).OrderBy(x => x));
    }
}
