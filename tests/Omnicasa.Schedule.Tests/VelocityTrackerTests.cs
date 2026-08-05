using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class VelocityTrackerTests
{
    [Fact]
    public void Velocity_TooFewSamples_IsZero()
    {
        var tracker = new VelocityTracker();
        Assert.Equal(0, tracker.Velocity(0));

        tracker.Add(0, 0);
        Assert.Equal(0, tracker.Velocity(0));
    }

    [Fact]
    public void Velocity_ConstantSpeed_RecoversItExactly()
    {
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            double t = i / 60.0;
            tracker.Add(t, t * 900.0);
        }

        Assert.Equal(900, tracker.Velocity(6 / 60.0), 3);
    }

    [Fact]
    public void Velocity_NegativeDirection_IsSigned()
    {
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            double t = i / 60.0;
            tracker.Add(t, t * -900.0);
        }

        Assert.Equal(-900, tracker.Velocity(6 / 60.0), 3);
    }

    [Fact]
    public void Velocity_IrregularSampleSpacing_StillRecoversTheSpeed()
    {
        // The whole point of the fit: touch samples do not arrive on a fixed cadence.
        var tracker = new VelocityTracker();
        double[] stamps = { 0.0, 0.004, 0.03, 0.035, 0.06, 0.078, 0.08 };
        foreach (double t in stamps)
        {
            tracker.Add(t, t * 1200.0);
        }

        Assert.Equal(1200, tracker.Velocity(0.08), 3);
    }

    [Fact]
    public void Velocity_AcceleratingFlick_ReportsTheSpeedAtRelease()
    {
        // p = 500t² → v(t) = 1000t, so at t = 0.1 the finger is doing 100 px/s, while its *average*
        // over the window is only 50. A release must fling at the former.
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 10; i++)
        {
            double t = i / 100.0;
            tracker.Add(t, 500 * t * t);
        }

        Assert.Equal(100, tracker.Velocity(0.1), 1);
    }

    [Fact]
    public void Velocity_FingerCameToRestBeforeLifting_IsZero()
    {
        // Drag, hold, lift. The samples are stale by the time of release, so there is no momentum.
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            double t = i / 60.0;
            tracker.Add(t, t * 900.0);
        }

        Assert.Equal(0, tracker.Velocity(0.5));
    }

    [Fact]
    public void Velocity_OnlyStaleSamplesInsideWindow_IgnoresOlderHistory()
    {
        // A fast drag that stops moving and then creeps: the fit must describe the creep, not the drag.
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 10; i++)
        {
            tracker.Add(i / 100.0, i * 30.0); // 3000 px/s
        }

        for (int i = 1; i <= 10; i++)
        {
            tracker.Add(0.1 + (i / 100.0), 300.0 + i); // 100 px/s
        }

        Assert.Equal(100, tracker.Velocity(0.2), 0);
    }

    [Fact]
    public void Velocity_ReversalAtTheEnd_DoesNotFlingBackwards()
    {
        // Noise that reverses direction must not produce a large opposite-sign fling: the quadratic
        // fit is only trusted while it agrees with the (stable) linear one.
        var tracker = new VelocityTracker();
        double[] positions = { 0, 10, 20, 30, 40, 41, 40 };
        for (int i = 0; i < positions.Length; i++)
        {
            tracker.Add(i / 100.0, positions[i]);
        }

        double v = tracker.Velocity(0.06);
        Assert.True(v >= 0, $"expected no backwards fling, got {v}");
    }

    [Fact]
    public void Velocity_StationaryFinger_IsZero()
    {
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            tracker.Add(i / 60.0, 42.0);
        }

        Assert.Equal(0, tracker.Velocity(6 / 60.0), 6);
    }

    [Fact]
    public void Velocity_MoreSamplesThanCapacity_UsesTheNewest()
    {
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 200; i++)
        {
            double t = i / 1000.0;
            tracker.Add(t, t * 750.0);
        }

        Assert.Equal(750, tracker.Velocity(0.2), 3);
    }

    [Fact]
    public void Clear_DropsHistory()
    {
        var tracker = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            double t = i / 60.0;
            tracker.Add(t, t * 900.0);
        }

        tracker.Clear();
        Assert.Equal(0, tracker.Velocity(6 / 60.0));
    }

    [Fact]
    public void Velocity_IdenticalGestureSampledDifferently_AgreesClosely()
    {
        // The regression this replaces: identical flicks flinging different distances because the
        // sample cadence differed. Both trackers see the same motion, one at 60Hz and one jittering.
        var steady = new VelocityTracker();
        for (int i = 0; i <= 6; i++)
        {
            double t = i / 60.0;
            steady.Add(t, t * 1500.0);
        }

        var jittery = new VelocityTracker();
        double[] stamps = { 0.0, 0.021, 0.026, 0.052, 0.07, 0.1 };
        foreach (double t in stamps)
        {
            jittery.Add(t, t * 1500.0);
        }

        double a = steady.Velocity(0.1);
        double b = jittery.Velocity(0.1);
        Assert.True(Math.Abs(a - b) < 1.0, $"{a} vs {b}");
    }
}
