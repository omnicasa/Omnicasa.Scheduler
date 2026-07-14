namespace Omnicasa.Schedule;

/// <summary>
/// Pure day-axis (horizontal) math for <c>InfiniteScheduleView</c>. The control owns a virtual
/// <c>horizontalOffset</c> in logical pixels measured from the anchor day's left edge, so the day
/// axis is effectively infinite: these helpers turn that offset into the visible day-index window
/// and back. Kept free of SkiaSharp / MAUI-UI types so it builds and unit-tests on the headless
/// net9.0 target.
/// </summary>
public static class InfiniteScheduleGeometry
{
    /// <summary>Zero-based index (relative to the anchor day) of the leftmost visible day.</summary>
    /// <param name="horizontalOffset">Pixels scrolled right of the anchor day's left edge.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    public static int FirstVisibleDay(double horizontalOffset, float dayWidth)
        => dayWidth <= 0 ? 0 : (int)Math.Floor(horizontalOffset / dayWidth);

    /// <summary>Number of day columns needed to fill <paramref name="bodyWidth"/>, plus one for the partial edge.</summary>
    /// <param name="bodyWidth">Width available for day columns (viewport minus the time rail).</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    public static int VisibleDayCount(float bodyWidth, float dayWidth)
        => dayWidth <= 0 ? 0 : (int)Math.Ceiling(bodyWidth / dayWidth) + 1;

    /// <summary>Snaps an offset to the nearest whole-day boundary (used to settle a fling on a day edge).</summary>
    /// <param name="horizontalOffset">Current offset in logical pixels.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    public static double SnapToDay(double horizontalOffset, float dayWidth)
        => dayWidth <= 0 ? horizontalOffset : Math.Round(horizontalOffset / dayWidth) * dayWidth;

    /// <summary>
    /// Clamps an offset so the day window stays within the optional min/max day indices. A null bound
    /// leaves that direction unbounded (truly infinite). Bounds are expressed as day indices relative
    /// to the anchor day.
    /// </summary>
    /// <param name="horizontalOffset">Requested offset in logical pixels.</param>
    /// <param name="bodyWidth">Width available for day columns.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    /// <param name="minDayIndex">Lowest reachable day index, or null for unbounded scroll-back.</param>
    /// <param name="maxDayIndex">Highest reachable day index, or null for unbounded scroll-forward.</param>
    public static double ClampOffset(
        double horizontalOffset,
        float bodyWidth,
        float dayWidth,
        int? minDayIndex,
        int? maxDayIndex)
    {
        if (dayWidth <= 0)
        {
            return horizontalOffset;
        }

        if (minDayIndex is int min)
        {
            horizontalOffset = Math.Max(horizontalOffset, min * (double)dayWidth);
        }

        if (maxDayIndex is int max)
        {
            // Keep the day after the last one from scrolling past the right edge.
            double maxOffset = ((max + 1) * (double)dayWidth) - bodyWidth;
            if (minDayIndex is int minGuard)
            {
                maxOffset = Math.Max(maxOffset, minGuard * (double)dayWidth);
            }

            horizontalOffset = Math.Min(horizontalOffset, maxOffset);
        }

        return horizontalOffset;
    }
}
