using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScrollPhysicsTests
{
    private const double Dimension = 800;

    [Fact]
    public void RubberBand_NoOverscroll_IsZero()
    {
        Assert.Equal(0, ScrollPhysics.RubberBand(0, Dimension));
        Assert.Equal(0, ScrollPhysics.RubberBand(-50, Dimension));
    }

    [Fact]
    public void RubberBand_ResistsMoreTheHarderItIsPulled()
    {
        // Doubling the pull must add less than double the travel, or it isn't resisting at all.
        double first = ScrollPhysics.RubberBand(100, Dimension);
        double second = ScrollPhysics.RubberBand(200, Dimension);

        Assert.True(first < 100);
        Assert.True(second < 2 * first);
    }

    [Fact]
    public void RubberBand_NeverReachesTheViewportExtent()
    {
        Assert.True(ScrollPhysics.RubberBand(100_000, Dimension) < Dimension);
    }

    [Fact]
    public void RubberBand_SmallPull_MovesAtTheBandConstant()
    {
        // The curve's slope at the edge is the constant itself, approached from below, so the content
        // gives immediately at ~55% of finger speed rather than sticking and then breaking free.
        double slope = ScrollPhysics.RubberBand(1, Dimension);
        Assert.InRange(slope, ScrollPhysics.DefaultRubberBandConstant * 0.99, ScrollPhysics.DefaultRubberBandConstant);
    }

    [Fact]
    public void RubberBand_DegenerateViewport_TracksOneToOne()
    {
        Assert.Equal(50, ScrollPhysics.RubberBand(50, 0));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(200)]
    [InlineData(3000)]
    public void RubberBandInverse_RoundTrips(double overscroll)
    {
        double shown = ScrollPhysics.RubberBand(overscroll, Dimension);
        Assert.Equal(overscroll, ScrollPhysics.RubberBandInverse(shown, Dimension), 6);
    }

    [Fact]
    public void ApplyRubberBand_InsideRange_IsUnchanged()
    {
        Assert.Equal(300, ScrollPhysics.ApplyRubberBand(300, 0, 1000, Dimension));
        Assert.Equal(0, ScrollPhysics.ApplyRubberBand(0, 0, 1000, Dimension));
        Assert.Equal(1000, ScrollPhysics.ApplyRubberBand(1000, 0, 1000, Dimension));
    }

    [Fact]
    public void ApplyRubberBand_PastEitherEdge_IsResistedSymmetrically()
    {
        double below = ScrollPhysics.ApplyRubberBand(-150, 0, 1000, Dimension);
        double above = ScrollPhysics.ApplyRubberBand(1150, 0, 1000, Dimension);

        Assert.True(below < 0 && below > -150);
        Assert.True(above > 1000 && above < 1150);
        Assert.Equal(-below, above - 1000, 6);
    }

    [Fact]
    public void ApplyRubberBand_NonScrollingContent_BandsAroundThePinnedOffset()
    {
        // Min == max (content shorter than the viewport): both directions still give elastically.
        Assert.True(ScrollPhysics.ApplyRubberBand(-100, 40, 40, Dimension) < 40);
        Assert.True(ScrollPhysics.ApplyRubberBand(300, 40, 40, Dimension) > 40);
    }

    [Theory]
    [InlineData(-400)]
    [InlineData(-30)]
    [InlineData(500)]
    [InlineData(1400)]
    public void RemoveRubberBand_RoundTripsApplyRubberBand(double raw)
    {
        double shown = ScrollPhysics.ApplyRubberBand(raw, 0, 1000, Dimension);
        Assert.Equal(raw, ScrollPhysics.RemoveRubberBand(shown, 0, 1000, Dimension), 6);
    }

    [Fact]
    public void SpringStep_ConvergesOnTheTarget()
    {
        double pos = 0, vel = 0;
        for (int i = 0; i < 200; i++)
        {
            (pos, vel) = ScrollPhysics.SpringStep(pos, vel, 500, 0.13, 1.0 / 60.0);
        }

        Assert.Equal(500, pos, 3);
        Assert.Equal(0, vel, 3);
    }

    [Fact]
    public void SpringStep_IsFrameRateIndependent()
    {
        // Same elapsed time, different step counts — the analytic solution must land in the same place.
        double coarse = 0, coarseV = 400;
        for (int i = 0; i < 6; i++)
        {
            (coarse, coarseV) = ScrollPhysics.SpringStep(coarse, coarseV, 500, 0.13, 1.0 / 60.0);
        }

        double fine = 0, fineV = 400;
        for (int i = 0; i < 12; i++)
        {
            (fine, fineV) = ScrollPhysics.SpringStep(fine, fineV, 500, 0.13, 1.0 / 120.0);
        }

        Assert.Equal(coarse, fine, 6);
        Assert.Equal(coarseV, fineV, 6);
    }

    [Fact]
    public void SpringStep_CarriesIncomingVelocityPastTheTarget()
    {
        // A bounce should overshoot and come back, not brake to the edge and stop.
        var (pos, _) = ScrollPhysics.SpringStep(0, -1500, 0, 0.13, 1.0 / 60.0);
        Assert.True(pos < 0);
    }

    [Fact]
    public void SpringStep_CriticallyDamped_DoesNotOscillate()
    {
        // Released from a displacement with no velocity: it must approach monotonically, never cross.
        double pos = -120, vel = 0;
        for (int i = 0; i < 200; i++)
        {
            (pos, vel) = ScrollPhysics.SpringStep(pos, vel, 0, 0.13, 1.0 / 60.0);
            Assert.True(pos <= 1e-9);
        }
    }

    [Fact]
    public void Decay_HalvesOverTheTimeConstantTimesLn2()
    {
        double v = ScrollPhysics.Decay(1000, 0.5, 0.5 * Math.Log(2));
        Assert.Equal(500, v, 6);
    }

    [Fact]
    public void Decay_IsFrameRateIndependent()
    {
        double coarse = 1000;
        for (int i = 0; i < 30; i++)
        {
            coarse = ScrollPhysics.Decay(coarse, 0.5, 1.0 / 60.0);
        }

        double fine = 1000;
        for (int i = 0; i < 60; i++)
        {
            fine = ScrollPhysics.Decay(fine, 0.5, 1.0 / 120.0);
        }

        Assert.Equal(coarse, fine, 6);
    }

    [Fact]
    public void IndicatorThumb_NonScrollingContent_IsHidden()
    {
        Assert.Null(ScrollPhysics.IndicatorThumb(0, 100, 100, 600));
        Assert.Null(ScrollPhysics.IndicatorThumb(0, 0, 500, 0));
    }

    [Fact]
    public void IndicatorThumb_SpansTheTrackFromTopToBottom()
    {
        var top = ScrollPhysics.IndicatorThumb(0, 0, 1000, 600);
        var bottom = ScrollPhysics.IndicatorThumb(1000, 0, 1000, 600);

        Assert.NotNull(top);
        Assert.NotNull(bottom);
        Assert.Equal(0, top!.Value.Top, 6);
        Assert.Equal(600 - bottom!.Value.Length, bottom.Value.Top, 6);
    }

    [Fact]
    public void IndicatorThumb_LengthIsTheVisibleFractionOfTheContent()
    {
        // 600 of 1600 visible → 600 * 600/1600 = 225.
        var thumb = ScrollPhysics.IndicatorThumb(0, 0, 1000, 600);
        Assert.Equal(225, thumb!.Value.Length, 6);
    }

    [Fact]
    public void IndicatorThumb_LongContent_StopsShrinkingAtTheFloor()
    {
        var thumb = ScrollPhysics.IndicatorThumb(0, 0, 500_000, 600, minLength: 36);
        Assert.Equal(36, thumb!.Value.Length, 6);
    }

    [Fact]
    public void IndicatorThumb_DuringOverscroll_StaysInsideTheTrack()
    {
        var above = ScrollPhysics.IndicatorThumb(-90, 0, 1000, 600);
        var below = ScrollPhysics.IndicatorThumb(1090, 0, 1000, 600);

        Assert.Equal(0, above!.Value.Top, 6);
        Assert.Equal(600 - below!.Value.Length, below!.Value.Top, 6);
    }
}
