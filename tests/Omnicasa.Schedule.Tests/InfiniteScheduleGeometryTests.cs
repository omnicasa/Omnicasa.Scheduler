using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class InfiniteScheduleGeometryTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 0)]
    [InlineData(100, 100, 1)]
    [InlineData(250, 100, 2)]
    [InlineData(-1, 100, -1)]
    [InlineData(-100, 100, -1)]
    public void FirstVisibleDay_FloorsOffsetByDayWidth(double offset, float dayWidth, int expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.FirstVisibleDay(offset, dayWidth));
    }

    [Fact]
    public void FirstVisibleDay_ZeroDayWidth_IsZero()
    {
        Assert.Equal(0, InfiniteScheduleGeometry.FirstVisibleDay(500, 0));
    }

    [Theory]
    [InlineData(300, 100, 4)] // 3 full + 1 partial-edge column
    [InlineData(320, 100, 5)]
    [InlineData(100, 100, 2)]
    public void VisibleDayCount_CoversBodyPlusOneEdge(float bodyWidth, float dayWidth, int expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.VisibleDayCount(bodyWidth, dayWidth));
    }

    [Fact]
    public void VisibleDayCount_ZeroDayWidth_IsZero()
    {
        Assert.Equal(0, InfiniteScheduleGeometry.VisibleDayCount(300, 0));
    }

    [Theory]
    [InlineData(40, 100, 0)]
    [InlineData(60, 100, 100)]
    [InlineData(149, 100, 100)]
    [InlineData(150, 100, 200)]
    public void SnapToDay_RoundsToNearestDayBoundary(double offset, float dayWidth, double expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.SnapToDay(offset, dayWidth), precision: 3);
    }

    [Fact]
    public void SnapToDay_ZeroDayWidth_ReturnsOffset()
    {
        Assert.Equal(123, InfiniteScheduleGeometry.SnapToDay(123, 0), precision: 3);
    }

    [Theory]
    [InlineData(0, 0, 0)]   // anchor day is the week's first cell
    [InlineData(3, 0, 3)]
    [InlineData(7, 0, 0)]   // wraps into the next week
    [InlineData(-1, 0, 6)]  // and back into the previous one
    [InlineData(-7, 0, 0)]
    [InlineData(0, 5, 5)]   // anchor sitting mid-week shifts every column
    [InlineData(2, 5, 0)]
    public void ColumnOfDay_WrapsBothDirections(int dayIndex, int anchorColumn, int expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.ColumnOfDay(dayIndex, anchorColumn));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(6, 0, 0)]
    [InlineData(7, 0, 1)]
    [InlineData(-1, 0, -1)]
    [InlineData(2, 5, 1)]
    public void WeekOfDay_FloorsIntoWeeks(int dayIndex, int anchorColumn, int expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.WeekOfDay(dayIndex, anchorColumn));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(5, 0)]
    [InlineData(-2, 6)]
    public void DayOfCell_InvertsWeekAndColumn(int dayIndex, int anchorColumn)
    {
        int week = InfiniteScheduleGeometry.WeekOfDay(dayIndex, anchorColumn);
        int column = InfiniteScheduleGeometry.ColumnOfDay(dayIndex, anchorColumn);
        Assert.Equal(dayIndex, InfiniteScheduleGeometry.DayOfCell(week, column, anchorColumn));
    }

    [Fact]
    public void WeekOfDay_HoldsTheWholeWeekOnOnePage()
    {
        // Every day of one week maps to the same page — the strip only swaps when the week does.
        int week = InfiniteScheduleGeometry.WeekOfDay(0, anchorColumn: 0);
        for (int dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            Assert.Equal(week, InfiniteScheduleGeometry.WeekOfDay(dayIndex, anchorColumn: 0));
        }

        Assert.NotEqual(week, InfiniteScheduleGeometry.WeekOfDay(7, anchorColumn: 0));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(250, 100, 2.5)]     // mid-swipe between day 2 and day 3
    [InlineData(700, 100, 7)]
    [InlineData(-50, 100, -0.5)]    // and the same going back before the anchor
    public void DayPosition_IsTheFractionalDayIndex(double offset, float dayWidth, double expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.DayPosition(offset, dayWidth), precision: 3);
    }

    [Fact]
    public void DayPosition_ZeroDayWidth_IsOrigin()
    {
        Assert.Equal(0, InfiniteScheduleGeometry.DayPosition(500, 0), precision: 3);
    }

    [Fact]
    public void CellEmphasis_PeaksOnTheCurrentDayAndDiesOffADayAway()
    {
        Assert.Equal(1.0, InfiniteScheduleGeometry.CellEmphasis(0), precision: 3);
        Assert.Equal(0.5, InfiniteScheduleGeometry.CellEmphasis(0.5), precision: 3);
        Assert.Equal(0.0, InfiniteScheduleGeometry.CellEmphasis(1), precision: 3);
        Assert.Equal(0.0, InfiniteScheduleGeometry.CellEmphasis(3.7), precision: 3);
    }

    [Fact]
    public void CellEmphasis_OnlyEverLitsTheTwoDaysAroundTheSwipe()
    {
        // Mid-swipe between day 2 and day 3, nothing else in the week may carry any marker.
        double position = InfiniteScheduleGeometry.DayPosition(250, 100);
        for (int dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            double emphasis = InfiniteScheduleGeometry.CellEmphasis(dayIndex - position);
            if (dayIndex is 2 or 3)
            {
                Assert.True(emphasis > 0);
            }
            else
            {
                Assert.Equal(0.0, emphasis, precision: 6);
            }
        }
    }

    [Fact]
    public void CellEmphasis_IsSymmetricAndBounded()
    {
        for (int step = -30; step <= 30; step++)
        {
            double d = step / 10.0;
            double emphasis = InfiniteScheduleGeometry.CellEmphasis(d);

            Assert.InRange(emphasis, 0.0, 1.0);
            Assert.Equal(InfiniteScheduleGeometry.CellEmphasis(-d), emphasis, precision: 6);
        }
    }

    [Fact]
    public void CellEmphasis_HandsFocusOverAcrossASwipe()
    {
        // Mid-swipe the two days straddling the middle share the focus evenly — neither jumps ahead.
        Assert.Equal(
            InfiniteScheduleGeometry.CellEmphasis(0.5),
            InfiniteScheduleGeometry.CellEmphasis(-0.5),
            precision: 6);

        // And the focus only ever grows on the way in.
        double previous = 0;
        for (int step = 10; step >= 0; step--)
        {
            double emphasis = InfiniteScheduleGeometry.CellEmphasis(step / 10.0);
            Assert.True(emphasis >= previous);
            previous = emphasis;
        }
    }

    [Fact]
    public void ClampOffset_NullBounds_IsUnbounded()
    {
        Assert.Equal(-5000, InfiniteScheduleGeometry.ClampOffset(-5000, 300, 100, null, null), precision: 3);
        Assert.Equal(9000, InfiniteScheduleGeometry.ClampOffset(9000, 300, 100, null, null), precision: 3);
    }

    [Fact]
    public void ClampOffset_MinDay_StopsScrollBack()
    {
        // minDayIndex 2 → the offset can't go below 2 * dayWidth.
        Assert.Equal(200, InfiniteScheduleGeometry.ClampOffset(50, 300, 100, minDayIndex: 2, maxDayIndex: null), precision: 3);
        Assert.Equal(500, InfiniteScheduleGeometry.ClampOffset(500, 300, 100, minDayIndex: 2, maxDayIndex: null), precision: 3);
    }

    [Fact]
    public void ClampOffset_MaxDay_KeepsLastDayOnScreen()
    {
        // maxDayIndex 9 → last day's right edge (10 * 100) can't scroll past the body (300 wide),
        // so the max offset is 10*100 - 300 = 700.
        Assert.Equal(700, InfiniteScheduleGeometry.ClampOffset(5000, 300, 100, minDayIndex: null, maxDayIndex: 9), precision: 3);
        Assert.Equal(400, InfiniteScheduleGeometry.ClampOffset(400, 300, 100, minDayIndex: null, maxDayIndex: 9), precision: 3);
    }

    [Fact]
    public void ClampOffset_ZeroDayWidth_ReturnsOffset()
    {
        Assert.Equal(123, InfiniteScheduleGeometry.ClampOffset(123, 300, 0, 0, 5), precision: 3);
    }

    [Theory]
    [InlineData(0, 0, true, 100)] // short flick from day 0 → day 1
    [InlineData(0, 60, true, 100)] // finger already dragged most of a day → still day 1
    [InlineData(0, 0, false, -100)] // backwards flick → day -1
    [InlineData(300, 340, true, 400)] // from day 3 → day 4
    [InlineData(300, 260, false, 200)] // from day 3 → day 2
    public void ChunkFlickTarget_AdvancesOneChunk(double start, double current, bool forward, double expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.ChunkFlickTarget(start, current, 100, 1, forward), precision: 3);
    }

    [Theory]
    [InlineData(0, 150, true, 200)] // fast flick dragged past day 1 → next whole day, not backwards
    [InlineData(0, 200, true, 200)] // landed exactly on day 2 → stay there
    [InlineData(0, -150, false, -200)]
    public void ChunkFlickTarget_NeverSettlesBehindTheFinger(double start, double current, bool forward, double expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.ChunkFlickTarget(start, current, 100, 1, forward), precision: 3);
    }

    [Fact]
    public void ChunkFlickTarget_WeekMode_StepsAScreenful()
    {
        // 7-day chunk: a flick from day 0 lands on day 7 (7 * 50 = 350).
        Assert.Equal(350, InfiniteScheduleGeometry.ChunkFlickTarget(0, 20, 50, 7, forward: true), precision: 3);
        Assert.Equal(-350, InfiniteScheduleGeometry.ChunkFlickTarget(0, -20, 50, 7, forward: false), precision: 3);
    }

    [Fact]
    public void ChunkFlickTarget_ZeroDayWidth_ReturnsCurrentOffset()
    {
        Assert.Equal(123, InfiniteScheduleGeometry.ChunkFlickTarget(0, 123, 0, 1, true), precision: 3);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(49, 100, 0)] // still mostly day 0
    [InlineData(50, 100, 1)] // past the midpoint → day 1 takes the header
    [InlineData(149, 100, 1)]
    [InlineData(-49, 100, 0)]
    [InlineData(-51, 100, -1)]
    public void DominantDay_FlipsAtTheMidpointOfASwipe(double offset, float dayWidth, int expected)
    {
        Assert.Equal(expected, InfiniteScheduleGeometry.DominantDay(offset, dayWidth));
    }

    [Fact]
    public void DominantDay_ZeroDayWidth_IsZero()
    {
        Assert.Equal(0, InfiniteScheduleGeometry.DominantDay(500, 0));
    }
}
