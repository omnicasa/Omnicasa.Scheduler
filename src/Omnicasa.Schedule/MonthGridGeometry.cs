namespace Omnicasa.Schedule;

/// <summary>One laid-out day cell: the date and the pixel rectangle it occupies.</summary>
public readonly struct MonthDayCell
{
    /// <summary>Initializes a new instance of the <see cref="MonthDayCell"/> struct.</summary>
    /// <param name="date">The calendar date this cell represents.</param>
    /// <param name="x">Left edge in logical pixels.</param>
    /// <param name="y">Top edge in logical pixels.</param>
    /// <param name="width">Cell width in logical pixels.</param>
    /// <param name="height">Cell height in logical pixels.</param>
    public MonthDayCell(DateOnly date, float x, float y, float width, float height)
    {
        Date = date;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the calendar date this cell represents.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets the left edge in logical pixels.</summary>
    public float X { get; }

    /// <summary>Gets the top edge in logical pixels.</summary>
    public float Y { get; }

    /// <summary>Gets the cell width in logical pixels.</summary>
    public float Width { get; }

    /// <summary>Gets the cell height in logical pixels.</summary>
    public float Height { get; }
}

/// <summary>
/// Pure vertical-axis + month-grid math for <c>InfiniteMonthCalendarView</c>. The control stacks
/// one uniform month block per month (block height = viewport, so exactly one month fills the
/// screen) and owns a virtual <c>verticalOffset</c> in logical pixels from the first block's top.
/// These helpers turn that offset into the visible block window and lay out a single month's grid.
/// Kept free of SkiaSharp / MAUI-UI types so it builds and unit-tests on the headless net9.0 target
/// (mirrors <see cref="InfiniteScheduleGeometry"/>).
/// </summary>
public static class MonthGridGeometry
{
    /// <summary>Zero-based index of the topmost visible month block.</summary>
    /// <param name="verticalOffset">Pixels scrolled below the first block's top edge.</param>
    /// <param name="blockHeight">Height of one month block in logical pixels.</param>
    public static int FirstVisibleBlock(double verticalOffset, float blockHeight)
        => blockHeight <= 0 ? 0 : (int)Math.Floor(verticalOffset / blockHeight);

    /// <summary>Number of month blocks needed to fill <paramref name="viewportHeight"/>, plus one for the partial edge.</summary>
    /// <param name="viewportHeight">Height available for month blocks.</param>
    /// <param name="blockHeight">Height of one month block in logical pixels.</param>
    public static int VisibleBlockCount(float viewportHeight, float blockHeight)
        => blockHeight <= 0 ? 0 : (int)Math.Ceiling(viewportHeight / blockHeight) + 1;

    /// <summary>Snaps an offset to the nearest whole-month boundary (used to settle a fling on a month edge).</summary>
    /// <param name="verticalOffset">Current offset in logical pixels.</param>
    /// <param name="blockHeight">Height of one month block in logical pixels.</param>
    public static double SnapToBlock(double verticalOffset, float blockHeight)
        => blockHeight <= 0 ? verticalOffset : Math.Round(verticalOffset / blockHeight) * blockHeight;

    /// <summary>
    /// Clamps an offset so the block window stays within the built range. Scroll-back stops at the
    /// first block; scroll-forward stops with the last block resting against the bottom edge (never
    /// negative, so a single short block still pins to the top).
    /// </summary>
    /// <param name="verticalOffset">Requested offset in logical pixels.</param>
    /// <param name="viewportHeight">Height available for month blocks.</param>
    /// <param name="blockHeight">Height of one month block in logical pixels.</param>
    /// <param name="blockCount">Total number of month blocks in the range.</param>
    public static double ClampOffset(double verticalOffset, float viewportHeight, float blockHeight, int blockCount)
    {
        if (blockHeight <= 0 || blockCount <= 0)
        {
            return Math.Max(0, verticalOffset);
        }

        double max = Math.Max(0, (blockCount * (double)blockHeight) - viewportHeight);
        return Math.Clamp(verticalOffset, 0, max);
    }

    /// <summary>Number of leading blank cells before day 1, given the week's first day (0–6).</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="firstDayOfWeek">The day the week starts on.</param>
    public static int FirstDayOffset(int year, int month, DayOfWeek firstDayOfWeek)
    {
        var first = new DateOnly(year, month, 1);
        return (((int)first.DayOfWeek - (int)firstDayOfWeek) + 7) % 7;
    }

    /// <summary>Number of week rows a month spans (4–6), given the week's first day.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="firstDayOfWeek">The day the week starts on.</param>
    public static int WeekRows(int year, int month, DayOfWeek firstDayOfWeek)
    {
        int offset = FirstDayOffset(year, month, firstDayOfWeek);
        int days = DateTime.DaysInMonth(year, month);
        return (int)Math.Ceiling((offset + days) / 7.0);
    }

    /// <summary>Zero-based block index of a month within a [minYear, 1] … range.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="minYear">First year in the range (block 0 is <paramref name="minYear"/>/January).</param>
    public static int BlockIndex(int year, int month, int minYear)
        => ((year - minYear) * 12) + (month - 1);

    /// <summary>
    /// Lays out a month's day cells inside a grid rectangle. The grid is seven equal columns and
    /// seven equal rows; row 0 is the weekday-heading row, so day cells start on row 1 and each day
    /// lands at its (column, week-row) slot. This is the layout both calendar views paint and hit-test
    /// against — keeping it here makes the day→rect mapping (and therefore tap targeting) testable.
    /// </summary>
    /// <param name="gridX">Left edge of the grid in logical pixels.</param>
    /// <param name="gridY">Top edge of the grid (the weekday row) in logical pixels.</param>
    /// <param name="gridWidth">Grid width in logical pixels (one column = a seventh of this).</param>
    /// <param name="gridHeight">Grid height in logical pixels (one row = a seventh of this).</param>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="firstDayOfWeek">The day the week starts on (column 0).</param>
    /// <returns>One <see cref="MonthDayCell"/> per day of the month, in day order.</returns>
    public static IReadOnlyList<MonthDayCell> DayCells(
        float gridX,
        float gridY,
        float gridWidth,
        float gridHeight,
        int year,
        int month,
        DayOfWeek firstDayOfWeek)
    {
        int offset = FirstDayOffset(year, month, firstDayOfWeek);
        int daysInMonth = DateTime.DaysInMonth(year, month);
        float cellW = gridWidth / 7f;
        float rowH = gridHeight / 7f;
        float daysTop = gridY + rowH; // row 0 is the weekday heading row

        var cells = new List<MonthDayCell>(daysInMonth);
        for (int d = 1; d <= daysInMonth; d++)
        {
            int idx = (d - 1) + offset;
            int row = idx / 7;
            int col = idx % 7;
            cells.Add(new MonthDayCell(
                new DateOnly(year, month, d),
                gridX + (col * cellW),
                daysTop + (row * rowH),
                cellW,
                rowH));
        }

        return cells;
    }
}
