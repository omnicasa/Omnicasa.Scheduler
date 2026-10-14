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

    /// <summary>
    /// Index of the day filling most of the viewport — the leftmost day rounded rather than floored.
    /// The one-day header is pinned to the viewport, not to a column, so it swaps content at the
    /// midpoint of a swipe instead of sliding away with the day it labels.
    /// </summary>
    /// <param name="horizontalOffset">Pixels scrolled right of the anchor day's left edge.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    public static int DominantDay(double horizontalOffset, float dayWidth)
        => dayWidth <= 0 ? 0 : (int)Math.Floor((horizontalOffset / dayWidth) + 0.5);

    /// <summary>Column (0..6) a day sits in inside its week.</summary>
    /// <param name="dayIndex">Day index relative to the anchor day.</param>
    /// <param name="anchorColumn">Column the anchor day itself occupies (0 = first day of the week).</param>
    public static int ColumnOfDay(int dayIndex, int anchorColumn)
    {
        int g = dayIndex + anchorColumn;
        return (int)(g - (7 * Math.Floor(g / 7.0)));
    }

    /// <summary>Index of the week a day belongs to (0 = the anchor day's week).</summary>
    /// <param name="dayIndex">Day index relative to the anchor day.</param>
    /// <param name="anchorColumn">Column the anchor day itself occupies (0 = first day of the week).</param>
    public static int WeekOfDay(int dayIndex, int anchorColumn)
        => (int)Math.Floor((dayIndex + anchorColumn) / 7.0);

    /// <summary>Day index of one cell of a week — the inverse of the two helpers above.</summary>
    /// <param name="weekIndex">Week index.</param>
    /// <param name="column">Column within the week (0..6).</param>
    /// <param name="anchorColumn">Column the anchor day itself occupies.</param>
    public static int DayOfCell(int weekIndex, int column, int anchorColumn)
        => (weekIndex * 7) + column - anchorColumn;

    /// <summary>
    /// The day axis as a continuous (fractional) day index — whole at rest, mid-way through a swipe.
    /// The day strip rides this directly, so it needs no animation state of its own: it tracks the
    /// finger, the fling and the day snap by construction and can never drift off the day below it.
    /// </summary>
    /// <param name="horizontalOffset">Pixels scrolled right of the anchor day's left edge.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    public static double DayPosition(double horizontalOffset, float dayWidth)
        => dayWidth <= 0 ? 0 : horizontalOffset / dayWidth;

    /// <summary>
    /// How strongly a strip cell is selected, given how many days it sits from the live day position:
    /// 1 for the day filling the viewport, 0 for anything a whole day away or further. Smoothstepped,
    /// so the marker eases from one day to the next instead of ramping at a constant rate. The strip
    /// itself never moves — this weight is what hands the marker over.
    /// </summary>
    /// <param name="distance">Signed distance from the current day position, in days.</param>
    public static double CellEmphasis(double distance)
    {
        double t = 1.0 - Math.Min(1.0, Math.Abs(distance));
        return t * t * (3.0 - (2.0 * t));
    }

    /// <summary>
    /// <see cref="CellEmphasis(double)"/> for a window <paramref name="span"/> days wide (two-day mode
    /// selects two cells at once): every day inside the window scores 1, and the fade runs over the one
    /// day either side of it. Identical to the single-day curve when <paramref name="span"/> is 1.
    /// </summary>
    /// <param name="distance">Signed distance from the window's first day, in days.</param>
    /// <param name="span">Days the window covers.</param>
    public static double CellEmphasis(double distance, int span)
    {
        double inner = Math.Max(1, span) - 1;
        return CellEmphasis(distance < 0 ? distance : Math.Max(0, distance - inner));
    }

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
    /// Target offset for a flick that advances one chunk (a screenful of days) from where the gesture
    /// started. A fast flick can drag the content past that chunk before the finger lifts, so the
    /// target is also pushed to the next whole day in the flick direction — the page transition never
    /// finishes behind where the finger left it.
    /// </summary>
    /// <param name="startOffset">Offset when the gesture began, in logical pixels.</param>
    /// <param name="currentOffset">Offset when the finger lifted, in logical pixels.</param>
    /// <param name="dayWidth">Width of one day column in logical pixels.</param>
    /// <param name="step">Days per chunk (the view mode).</param>
    /// <param name="forward">True when the flick moves the content to later days.</param>
    public static double ChunkFlickTarget(
        double startOffset,
        double currentOffset,
        float dayWidth,
        int step,
        bool forward)
    {
        if (dayWidth <= 0)
        {
            return currentOffset;
        }

        const double Tolerance = 0.001; // don't let float dust on a day boundary cost a whole extra day
        int startDay = (int)Math.Round(startOffset / dayWidth);
        double current = currentOffset / dayWidth;
        int target = forward
            ? Math.Max(startDay + Math.Max(1, step), (int)Math.Ceiling(current - Tolerance))
            : Math.Min(startDay - Math.Max(1, step), (int)Math.Floor(current + Tolerance));

        return target * (double)dayWidth;
    }

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

    /// <summary>
    /// Whether the view holds touches the platform no longer tracks — a system gesture took them
    /// without sending an up/cancel. MOBILE-12753.
    /// </summary>
    /// <param name="trackedTouches">Touches the view still has down.</param>
    /// <param name="liveTouches">Touches the native recognizer reports; zero or less when unknown.</param>
    public static bool HasLostTouches(int trackedTouches, int liveTouches)
        => liveTouches > 0 && trackedTouches > liveTouches;
}
