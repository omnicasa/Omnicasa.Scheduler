namespace Omnicasa.Schedule;

/// <summary>Converts between times of day and Y coordinates on the day-view canvas.</summary>
public readonly struct TimeScale
{
    /// <summary>Initializes a new instance of the <see cref="TimeScale"/> struct.</summary>
    /// <param name="hourHeight">The vertical size of one hour in logical pixels.</param>
    /// <param name="topPadding">Optional padding at the top of the canvas, in logical pixels.</param>
    /// <param name="bottomPadding">Optional padding below the last hour, in logical pixels.</param>
    /// <param name="lockStartHour">First hour the user may scroll to (0 = no lock).</param>
    /// <param name="lockEndHour">Last hour the user may scroll to (24 = no lock).</param>
    public TimeScale(
        float hourHeight,
        float topPadding = 0,
        float bottomPadding = 0,
        float lockStartHour = 0,
        float lockEndHour = 24)
    {
        HourHeight = hourHeight;
        TopPadding = topPadding;
        BottomPadding = bottomPadding;

        float start = Math.Clamp(lockStartHour, 0f, 24f);
        float end = Math.Clamp(lockEndHour, 0f, 24f);

        // A degenerate or inverted window would trap the view on one row — treat it as "no lock".
        if (end <= start)
        {
            start = 0f;
            end = 24f;
        }

        LockStartHour = start;
        LockEndHour = end;
    }

    /// <summary>Gets the vertical size of one hour in logical pixels.</summary>
    public float HourHeight { get; }

    /// <summary>Gets the padding applied at the top of the canvas, in logical pixels.</summary>
    public float TopPadding { get; }

    /// <summary>Gets the padding applied below the 24:00 line, in logical pixels.</summary>
    public float BottomPadding { get; }

    /// <summary>Gets the first hour the user may scroll to; 0 when no working-time lock is set.</summary>
    public float LockStartHour { get; }

    /// <summary>Gets the last hour the user may scroll to; 24 when no working-time lock is set.</summary>
    public float LockEndHour { get; }

    /// <summary>Gets a value indicating whether a working-time lock narrower than the full day is set.</summary>
    public bool IsLocked => LockStartHour > 0 || LockEndHour < 24;

    /// <summary>Gets the total height required to render a full 24-hour day.</summary>
    public float TotalHeight => TopPadding + (HourHeight * 24) + BottomPadding;

    /// <summary>Gets the Y of the locked window's top edge; the top padding is kept when unlocked.</summary>
    public float LockTop => LockStartHour <= 0 ? 0 : YForTime(TimeSpan.FromHours(LockStartHour));

    /// <summary>Gets the Y of the locked window's bottom edge; the bottom padding is kept when unlocked.</summary>
    public float LockBottom => LockEndHour >= 24 ? TotalHeight : YForTime(TimeSpan.FromHours(LockEndHour));

    /// <summary>Returns the Y coordinate for the given time of day.</summary>
    /// <param name="t">Time of day.</param>
    /// <returns>Y coordinate in logical pixels.</returns>
    public float YForTime(TimeSpan t) => TopPadding + (float)(t.TotalHours * HourHeight);

    /// <summary>Returns the Y coordinate for the time-of-day portion of <paramref name="t"/>.</summary>
    /// <param name="t">Date and time.</param>
    /// <returns>Y coordinate in logical pixels.</returns>
    public float YForTime(DateTime t) => YForTime(t.TimeOfDay);

    /// <summary>Returns the time of day represented by the given Y coordinate.</summary>
    /// <param name="y">Y coordinate in logical pixels.</param>
    /// <returns>Time of day.</returns>
    public TimeSpan TimeForY(float y) => TimeSpan.FromHours(Math.Max(0, (y - TopPadding) / HourHeight));

    /// <summary>
    /// The range of vertical scroll offsets that keeps the viewport inside the locked window. With no
    /// lock this is the ordinary "0 to content height minus viewport". A canvas that owns its own
    /// scrolling needs the edges themselves, not just a clamp, to rubber-band and bounce against them.
    /// </summary>
    /// <param name="viewportHeight">Height of the scrolling viewport in logical pixels.</param>
    /// <returns>The lowest and highest in-range offsets; equal when the content does not scroll.</returns>
    public (double Min, double Max) ScrollRange(float viewportHeight)
    {
        double contentMax = Math.Max(0, TotalHeight - viewportHeight);
        double lo = Math.Clamp(LockTop, 0, contentMax);
        double hi = Math.Clamp(LockBottom - viewportHeight, 0, contentMax);

        // Window shorter than the viewport: pin it centered rather than jamming it against one edge.
        if (hi <= lo)
        {
            double pinned = Math.Clamp((LockTop + LockBottom - viewportHeight) / 2, 0, contentMax);
            return (pinned, pinned);
        }

        return (lo, hi);
    }

    /// <summary>Clamps a vertical scroll offset to <see cref="ScrollRange"/>.</summary>
    /// <param name="offset">Proposed scroll offset in logical pixels.</param>
    /// <param name="viewportHeight">Height of the scrolling viewport in logical pixels.</param>
    /// <returns>The offset clamped to the scrollable range.</returns>
    public double ClampOffset(double offset, float viewportHeight)
    {
        var (min, max) = ScrollRange(viewportHeight);
        return Math.Clamp(offset, min, max);
    }

    /// <summary>Returns the locked window's start on <paramref name="day"/> (midnight when unlocked).</summary>
    /// <param name="day">Any time within the target day.</param>
    /// <returns>The earliest allowed time on that day.</returns>
    public DateTime WindowStart(DateTime day) => day.Date.AddHours(LockStartHour);

    /// <summary>Returns the locked window's end on <paramref name="day"/> (next midnight when unlocked).</summary>
    /// <param name="day">Any time within the target day.</param>
    /// <returns>The latest allowed time on that day.</returns>
    public DateTime WindowEnd(DateTime day) => day.Date.AddHours(LockEndHour);

    /// <summary>Clamps a time of day into the locked window.</summary>
    /// <param name="t">Time of day.</param>
    /// <returns>The time clamped to the locked window.</returns>
    public TimeSpan ClampToWindow(TimeSpan t)
        => TimeSpan.FromHours(Math.Clamp(t.TotalHours, LockStartHour, LockEndHour));

    /// <summary>Keeps <c>[start, start + duration]</c> inside the locked window of <paramref name="day"/>.</summary>
    /// <param name="start">Proposed start time.</param>
    /// <param name="day">Any time within the target day.</param>
    /// <param name="duration">Length of the appointment.</param>
    /// <returns>The start time clamped so the whole appointment fits the window.</returns>
    public DateTime ClampToWindow(DateTime start, DateTime day, TimeSpan duration)
    {
        var lo = WindowStart(day);
        var hi = WindowEnd(day) - duration;
        if (hi < lo)
        {
            hi = lo;
        }

        return start < lo ? lo : start > hi ? hi : start;
    }
}
