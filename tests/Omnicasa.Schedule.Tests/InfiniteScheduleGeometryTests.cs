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
}
