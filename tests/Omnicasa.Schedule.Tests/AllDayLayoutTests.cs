using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class AllDayLayoutTests
{
    private static readonly DateOnly RangeStart = new DateOnly(2026, 5, 25);

    private static TestScheduleItem Timed(DateTime start, DateTime end, string id) =>
        new TestScheduleItem { Id = id, Start = start, End = end };

    [Fact]
    public void IsSpanning_AllDayItem_IsTrue()
    {
        var item = new TestScheduleItem
        {
            Id = "a",
            IsAllDay = true,
            Start = new DateTime(2026, 5, 25, 0, 0, 0),
            End = new DateTime(2026, 5, 25, 23, 59, 0),
        };

        Assert.True(AllDayLayout.IsSpanning(item));
    }

    [Fact]
    public void IsSpanning_MultiDayTimedItem_IsTrue()
    {
        var item = Timed(new DateTime(2026, 5, 25, 14, 0, 0), new DateTime(2026, 5, 27, 10, 0, 0), "trip");
        Assert.True(AllDayLayout.IsSpanning(item));
    }

    [Fact]
    public void IsSpanning_SingleDayTimedItem_IsFalse()
    {
        var item = Timed(new DateTime(2026, 5, 25, 9, 0, 0), new DateTime(2026, 5, 25, 10, 0, 0), "meeting");
        Assert.False(AllDayLayout.IsSpanning(item));
    }

    [Fact]
    public void IsSpanning_EndsAtMidnight_TreatedAsSingleDay()
    {
        // [25 09:00, 26 00:00) ends exactly at midnight -> spans only the 25th.
        var item = Timed(new DateTime(2026, 5, 25, 9, 0, 0), new DateTime(2026, 5, 26, 0, 0, 0), "evening");
        Assert.False(AllDayLayout.IsSpanning(item));
    }

    [Fact]
    public void Layout_MapsAndClampsDaySpanToVisibleRange()
    {
        // Spans 24th..27th; visible range is 25th..(25th+5) -> clamped to days 0..2.
        var item = new TestScheduleItem
        {
            Id = "trip",
            IsAllDay = true,
            Start = new DateTime(2026, 5, 24, 0, 0, 0),
            End = new DateTime(2026, 5, 27, 23, 0, 0),
        };

        var bars = AllDayLayout.Layout(new IScheduleItem[] { item }, RangeStart, days: 5);

        var bar = Assert.Single(bars);
        Assert.Equal(0, bar.StartDay);   // 24th clamped to 0
        Assert.Equal(2, bar.EndDay);     // 27th -> day index 2
        Assert.Equal(0, bar.Lane);
    }

    [Fact]
    public void Layout_DropsItemsEntirelyOutsideRange()
    {
        var before = Timed(new DateTime(2026, 5, 1, 0, 0, 0), new DateTime(2026, 5, 2, 0, 0, 0), "old");
        var bars = AllDayLayout.Layout(new IScheduleItem[] { before }, RangeStart, days: 5);
        Assert.Empty(bars);
    }

    [Fact]
    public void Layout_OverlappingBarsGetSeparateLanes_NonOverlappingShareLane()
    {
        IScheduleItem AllDay(int startDay, int endDay, string id) => new TestScheduleItem
        {
            Id = id,
            IsAllDay = true,
            Start = RangeStart.AddDays(startDay).ToDateTime(TimeOnly.MinValue),
            End = RangeStart.AddDays(endDay).ToDateTime(new TimeOnly(23, 0)),
        };

        // a: days 0..1, b: days 1..2 (overlaps a on day1 -> lane 1), c: days 3..3 (no overlap -> lane 0).
        var bars = AllDayLayout.Layout(new[] { AllDay(0, 1, "a"), AllDay(1, 2, "b"), AllDay(3, 3, "c") }, RangeStart, days: 5);

        int LaneOf(string id) => bars.Single(b => b.Item.Id == id).Lane;
        Assert.Equal(0, LaneOf("a"));
        Assert.Equal(1, LaneOf("b"));
        Assert.Equal(0, LaneOf("c"));
    }
}
