namespace Omnicasa.Schedule;

/// <summary>Converts between times of day and Y coordinates on the day-view canvas.</summary>
public readonly struct TimeScale
{
    /// <summary>Initializes a new instance of the <see cref="TimeScale"/> struct.</summary>
    /// <param name="hourHeight">The vertical size of one hour in logical pixels.</param>
    /// <param name="topPadding">Optional padding at the top of the canvas, in logical pixels.</param>
    /// <param name="bottomPadding">Optional padding below the last hour, in logical pixels.</param>
    public TimeScale(float hourHeight, float topPadding = 0, float bottomPadding = 0)
    {
        HourHeight = hourHeight;
        TopPadding = topPadding;
        BottomPadding = bottomPadding;
    }

    /// <summary>Gets the vertical size of one hour in logical pixels.</summary>
    public float HourHeight { get; }

    /// <summary>Gets the padding applied at the top of the canvas, in logical pixels.</summary>
    public float TopPadding { get; }

    /// <summary>Gets the padding applied below the 24:00 line, in logical pixels.</summary>
    public float BottomPadding { get; }

    /// <summary>Gets the total height required to render a full 24-hour day.</summary>
    public float TotalHeight => TopPadding + (HourHeight * 24) + BottomPadding;

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
}
