namespace Omnicasa.Schedule;

/// <summary>
/// Pure scrolling physics for the schedule canvases, which own their scroll offsets instead of
/// sitting in a native <c>ScrollView</c>: the rubber-band curve that resists a pull past an edge,
/// the critically damped spring that settles an offset, frame-rate-independent fling decay, and
/// scroll-indicator geometry. Kept free of SkiaSharp / MAUI-UI types so it builds and unit-tests on
/// the headless target.
/// </summary>
public static class ScrollPhysics
{
    /// <summary>Stiffness of the rubber band; 0.55 is the constant UIScrollView uses.</summary>
    public const double DefaultRubberBandConstant = 0.55;

    /// <summary>
    /// Visible travel for a pull of <paramref name="overscroll"/> pixels past an edge. The curve is
    /// asymptotic — the content never moves further than <paramref name="dimension"/> — so resistance
    /// grows the harder the edge is pulled, which is what makes the end of a scroll feel elastic
    /// rather than walled.
    /// </summary>
    /// <param name="overscroll">Requested travel past the edge, in logical pixels.</param>
    /// <param name="dimension">Viewport extent along the scrolling axis, in logical pixels.</param>
    /// <param name="constant">Band stiffness; smaller resists harder.</param>
    /// <returns>The travel actually shown, always less than <paramref name="dimension"/>.</returns>
    public static double RubberBand(
        double overscroll,
        double dimension,
        double constant = DefaultRubberBandConstant)
    {
        if (overscroll <= 0)
        {
            return 0;
        }

        // Degenerate viewport or stiffness: no band to apply, so track the finger 1:1.
        if (dimension <= 0 || constant <= 0)
        {
            return overscroll;
        }

        return (1.0 - (1.0 / (((overscroll * constant) / dimension) + 1.0))) * dimension;
    }

    /// <summary>
    /// Inverse of <see cref="RubberBand"/>: the pull that would show <paramref name="shown"/> pixels
    /// of travel. Needed when a gesture starts on already-banded content, so the drag resumes from
    /// the same pull instead of jumping.
    /// </summary>
    /// <param name="shown">Visible travel past the edge, in logical pixels.</param>
    /// <param name="dimension">Viewport extent along the scrolling axis, in logical pixels.</param>
    /// <param name="constant">Band stiffness used to produce <paramref name="shown"/>.</param>
    /// <returns>The unbanded pull past the edge.</returns>
    public static double RubberBandInverse(
        double shown,
        double dimension,
        double constant = DefaultRubberBandConstant)
    {
        if (shown <= 0)
        {
            return 0;
        }

        if (dimension <= 0 || constant <= 0)
        {
            return shown;
        }

        // The band asymptotes at `dimension`, so anything at or past it came from an unbounded pull.
        return shown >= dimension ? double.MaxValue : (shown * dimension) / (constant * (dimension - shown));
    }

    /// <summary>
    /// Applies <see cref="RubberBand"/> to an unclamped offset: inside <c>[min, max]</c> the offset
    /// passes through untouched, outside it the excess is banded.
    /// </summary>
    /// <param name="offset">Requested (unbanded) offset in logical pixels.</param>
    /// <param name="min">Lowest in-range offset.</param>
    /// <param name="max">Highest in-range offset; equal to <paramref name="min"/> when nothing scrolls.</param>
    /// <param name="dimension">Viewport extent along the scrolling axis, in logical pixels.</param>
    /// <param name="constant">Band stiffness.</param>
    /// <returns>The offset to display.</returns>
    public static double ApplyRubberBand(
        double offset,
        double min,
        double max,
        double dimension,
        double constant = DefaultRubberBandConstant)
    {
        if (max < min)
        {
            max = min;
        }

        if (offset < min)
        {
            return min - RubberBand(min - offset, dimension, constant);
        }

        if (offset > max)
        {
            return max + RubberBand(offset - max, dimension, constant);
        }

        return offset;
    }

    /// <summary>Inverse of <see cref="ApplyRubberBand"/>, for resuming a drag on banded content.</summary>
    /// <param name="shown">Currently displayed offset in logical pixels.</param>
    /// <param name="min">Lowest in-range offset.</param>
    /// <param name="max">Highest in-range offset.</param>
    /// <param name="dimension">Viewport extent along the scrolling axis, in logical pixels.</param>
    /// <param name="constant">Band stiffness used to produce <paramref name="shown"/>.</param>
    /// <returns>The unbanded offset behind <paramref name="shown"/>.</returns>
    public static double RemoveRubberBand(
        double shown,
        double min,
        double max,
        double dimension,
        double constant = DefaultRubberBandConstant)
    {
        if (max < min)
        {
            max = min;
        }

        if (shown < min)
        {
            return min - RubberBandInverse(min - shown, dimension, constant);
        }

        if (shown > max)
        {
            return max + RubberBandInverse(shown - max, dimension, constant);
        }

        return shown;
    }

    /// <summary>
    /// One exact step of a critically damped spring — the analytic solution, not an Euler step, so
    /// the result is identical whatever the frame rate and the incoming velocity flows into the
    /// animation instead of restarting from zero.
    /// </summary>
    /// <param name="position">Current position in logical pixels.</param>
    /// <param name="velocity">Current velocity in logical pixels per second.</param>
    /// <param name="target">Position the spring is pulling toward.</param>
    /// <param name="timeConstant">Seconds for the spring to cover most of the remaining distance.</param>
    /// <param name="dt">Elapsed time for this frame, in seconds.</param>
    /// <returns>The stepped position and velocity.</returns>
    public static (double Position, double Velocity) SpringStep(
        double position,
        double velocity,
        double target,
        double timeConstant,
        double dt)
    {
        if (timeConstant <= 0)
        {
            return (target, 0);
        }

        double omega = 1.0 / timeConstant;
        double x = position - target;
        double c = velocity + (omega * x);
        double e = Math.Exp(-omega * dt);
        double next = (x + (c * dt)) * e;

        return (target + next, (c - (omega * (x + (c * dt)))) * e);
    }

    /// <summary>Frame-rate-independent exponential decay of a fling velocity.</summary>
    /// <param name="velocity">Current velocity in logical pixels per second.</param>
    /// <param name="timeConstant">Decay time constant in seconds (0.5 matches UIScrollView).</param>
    /// <param name="dt">Elapsed time for this frame, in seconds.</param>
    /// <returns>The decayed velocity.</returns>
    public static double Decay(double velocity, double timeConstant, double dt)
        => timeConstant <= 0 ? 0 : velocity * Math.Exp(-dt / timeConstant);

    /// <summary>
    /// Track position and length of the scroll indicator thumb, in logical pixels from the top of the
    /// viewport. Returns null when the content fits and there is nothing to indicate.
    /// </summary>
    /// <param name="offset">Current scroll offset (may be outside the range during a bounce).</param>
    /// <param name="min">Lowest in-range offset.</param>
    /// <param name="max">Highest in-range offset.</param>
    /// <param name="viewportHeight">Height of the scrolling viewport, in logical pixels.</param>
    /// <param name="minLength">Shortest the thumb may shrink to, in logical pixels.</param>
    /// <returns>The thumb's top and length, or null when the content does not scroll.</returns>
    public static (double Top, double Length)? IndicatorThumb(
        double offset,
        double min,
        double max,
        double viewportHeight,
        double minLength = 36)
    {
        double range = max - min;
        if (range <= 0 || viewportHeight <= 0)
        {
            return null;
        }

        double contentHeight = viewportHeight + range;
        double length = Math.Clamp((viewportHeight * viewportHeight) / contentHeight, minLength, viewportHeight);
        double travel = viewportHeight - length;
        double fraction = Math.Clamp((offset - min) / range, 0.0, 1.0);

        return (fraction * travel, length);
    }
}
