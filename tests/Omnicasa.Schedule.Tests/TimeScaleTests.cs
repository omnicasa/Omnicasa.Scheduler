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

    [Fact]
    public void TotalHeight_IncludesBottomPadding()
    {
        var scale = new TimeScale(60, topPadding: 48, bottomPadding: 32);
        Assert.Equal(48 + (60 * 24) + 32, scale.TotalHeight);
    }

    [Fact]
    public void BottomPadding_DoesNotShiftTimeMapping()
    {
        var scale = new TimeScale(60, topPadding: 48, bottomPadding: 32);
        Assert.Equal(48, scale.YForTime(TimeSpan.Zero), precision: 3);
        Assert.Equal(48 + (60 * 24), scale.YForTime(TimeSpan.FromHours(24)), precision: 3);
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
    public void ClampOffset_Unlocked_UsesFullContentRange()
    {
        var scale = new TimeScale(60);
        Assert.Equal(0, scale.ClampOffset(-50, 400), precision: 3);
        Assert.Equal(600, scale.ClampOffset(600, 400), precision: 3);
        Assert.Equal((60 * 24) - 400, scale.ClampOffset(99999, 400), precision: 3);
    }

    [Fact]
    public void ClampOffset_Locked_KeepsViewportInsideWindow()
    {
        // 08:00–18:00 at 60px/h: window spans y 480..1080, viewport 400 tall.
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        Assert.Equal(480, scale.ClampOffset(0, 400), precision: 3);
        Assert.Equal(600, scale.ClampOffset(600, 400), precision: 3);
        Assert.Equal(680, scale.ClampOffset(99999, 400), precision: 3);
    }

    [Fact]
    public void ClampOffset_LockedWindowShorterThanViewport_CentersTheWindow()
    {
        // 600px window, 800px viewport: pin it centered instead of against an edge.
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        Assert.Equal(380, scale.ClampOffset(0, 800), precision: 3);
        Assert.Equal(380, scale.ClampOffset(99999, 800), precision: 3);
    }

    [Fact]
    public void ClampOffset_LockedWithTopPadding_IncludesPaddingInWindowTop()
    {
        var scale = new TimeScale(60, topPadding: 48, lockStartHour: 8, lockEndHour: 18);
        Assert.Equal(48 + 480, scale.ClampOffset(0, 400), precision: 3);
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(18, 8)]
    [InlineData(8, 8)]
    public void Ctor_DegenerateOrInvertedWindow_IsTreatedAsUnlocked(float start, float end)
    {
        var scale = new TimeScale(60, lockStartHour: start, lockEndHour: end);
        Assert.False(scale.IsLocked);
        Assert.Equal(0, scale.LockStartHour);
        Assert.Equal(24, scale.LockEndHour);
    }

    [Fact]
    public void ClampToWindow_TimeOfDay_ClampsBothEnds()
    {
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        Assert.Equal(TimeSpan.FromHours(8), scale.ClampToWindow(TimeSpan.FromHours(3)));
        Assert.Equal(TimeSpan.FromHours(12), scale.ClampToWindow(TimeSpan.FromHours(12)));
        Assert.Equal(TimeSpan.FromHours(18), scale.ClampToWindow(TimeSpan.FromHours(22)));
    }

    [Fact]
    public void ClampToWindow_KeepsWholeAppointmentInsideWindow()
    {
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        var day = new DateTime(2026, 8, 3);
        var duration = TimeSpan.FromHours(2);

        Assert.Equal(day.AddHours(8), scale.ClampToWindow(day.AddHours(5), day, duration));
        Assert.Equal(day.AddHours(10), scale.ClampToWindow(day.AddHours(10), day, duration));

        // End is pulled back so the 2h block finishes at 18:00, not past it.
        Assert.Equal(day.AddHours(16), scale.ClampToWindow(day.AddHours(23), day, duration));
    }

    [Fact]
    public void ClampToWindow_Unlocked_MatchesFullDayBounds()
    {
        var scale = new TimeScale(60);
        var day = new DateTime(2026, 8, 3);
        var duration = TimeSpan.FromHours(2);

        Assert.Equal(day, scale.ClampToWindow(day.AddHours(-3), day, duration));
        Assert.Equal(day.AddHours(22), scale.ClampToWindow(day.AddHours(30), day, duration));
    }

    [Fact]
    public void ClampToWindow_DurationLongerThanWindow_PinsToWindowStart()
    {
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        var day = new DateTime(2026, 8, 3);
        Assert.Equal(day.AddHours(8), scale.ClampToWindow(day.AddHours(12), day, TimeSpan.FromHours(14)));
    }

    [Fact]
    public void TimeForY_ClampsAbovePaddingToZero()
    {
        var scale = new TimeScale(60, topPadding: 48);
        Assert.Equal(TimeSpan.Zero, scale.TimeForY(0));
        Assert.Equal(TimeSpan.Zero, scale.TimeForY(-100));
    }

    [Fact]
    public void ScrollRange_Unlocked_SpansZeroToContentMinusViewport()
    {
        var scale = new TimeScale(60);
        var (min, max) = scale.ScrollRange(400);

        Assert.Equal(0, min, precision: 3);
        Assert.Equal((60 * 24) - 400, max, precision: 3);
    }

    [Fact]
    public void ScrollRange_Locked_StopsAtTheWindowEdges()
    {
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        var (min, max) = scale.ScrollRange(400);

        Assert.Equal(480, min, precision: 3);
        Assert.Equal(680, max, precision: 3);
    }

    [Fact]
    public void ScrollRange_WindowShorterThanViewport_CollapsesToThePinnedOffset()
    {
        var scale = new TimeScale(60, lockStartHour: 8, lockEndHour: 18);
        var (min, max) = scale.ScrollRange(800);

        Assert.Equal(380, min, precision: 3);
        Assert.Equal(min, max, precision: 3);
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(8, 400)]
    [InlineData(8, 800)]
    public void ScrollRange_AgreesWithClampOffset(float lockStart, float viewport)
    {
        // The canvas rubber-bands against these edges, so they must be the same ones ClampOffset uses.
        var scale = new TimeScale(60, lockStartHour: lockStart, lockEndHour: lockStart == 0 ? 24 : 18);
        var (min, max) = scale.ScrollRange(viewport);

        Assert.Equal(min, scale.ClampOffset(-99999, viewport), precision: 3);
        Assert.Equal(max, scale.ClampOffset(99999, viewport), precision: 3);
    }
}
