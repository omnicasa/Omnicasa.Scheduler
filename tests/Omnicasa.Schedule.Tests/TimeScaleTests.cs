using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class TimeScaleTests
{
    [Fact]
    public void TotalHeight_IsTwentyFourHoursPlusPadding()
    {
        var scale = new TimeScale(60, topPadding: 48);
        Assert.Equal(48 + (60 * 24), scale.TotalHeight);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 60)]
    [InlineData(12, 720)]
    [InlineData(24, 1440)]
    public void YForTime_NoPadding_ScalesByHourHeight(double hours, float expectedY)
    {
        var scale = new TimeScale(60);
        Assert.Equal(expectedY, scale.YForTime(TimeSpan.FromHours(hours)), precision: 3);
    }

    [Fact]
    public void YForTime_AddsTopPadding()
    {
        var scale = new TimeScale(60, topPadding: 48);
        Assert.Equal(48, scale.YForTime(TimeSpan.Zero), precision: 3);
        Assert.Equal(48 + 90, scale.YForTime(TimeSpan.FromHours(1.5)), precision: 3);
    }

    [Fact]
    public void YForTime_DateTimeOverload_UsesTimeOfDayOnly()
    {
        var scale = new TimeScale(60);
        var dt = new DateTime(2026, 5, 25, 9, 30, 0);
        Assert.Equal(scale.YForTime(TimeSpan.FromHours(9.5)), scale.YForTime(dt), precision: 3);
    }

    [Fact]
    public void TimeForY_IsInverseOfYForTime()
    {
        var scale = new TimeScale(80, topPadding: 20);
        var time = TimeSpan.FromHours(7.25);
        float y = scale.YForTime(time);
        Assert.Equal(time.TotalHours, scale.TimeForY(y).TotalHours, precision: 3);
    }

    [Fact]
    public void TimeForY_ClampsAbovePaddingToZero()
    {
        var scale = new TimeScale(60, topPadding: 48);
        Assert.Equal(TimeSpan.Zero, scale.TimeForY(0));
        Assert.Equal(TimeSpan.Zero, scale.TimeForY(-100));
    }
}
