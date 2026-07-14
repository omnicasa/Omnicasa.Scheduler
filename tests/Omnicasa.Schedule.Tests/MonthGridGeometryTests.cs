using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class MonthGridGeometryTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(99, 100, 0)]
    [InlineData(100, 100, 1)]
    [InlineData(250, 100, 2)]
    [InlineData(-1, 100, -1)]   // scrolled above the first block
    public void FirstVisibleBlock_FloorsOffsetByBlockHeight(double offset, float blockHeight, int expected)
        => Assert.Equal(expected, MonthGridGeometry.FirstVisibleBlock(offset, blockHeight));

    [Fact]
    public void FirstVisibleBlock_ZeroHeight_IsZero()
        => Assert.Equal(0, MonthGridGeometry.FirstVisibleBlock(500, 0));

    [Theory]
    [InlineData(600, 600, 2)]   // exact fit still needs the partial-edge +1
    [InlineData(650, 600, 3)]   // ceil(650/600)=2, +1
    [InlineData(1250, 600, 4)]  // ceil(1250/600)=3, +1
    public void VisibleBlockCount_CoversViewportPlusPartialEdge(float viewport, float blockHeight, int expected)
        => Assert.Equal(expected, MonthGridGeometry.VisibleBlockCount(viewport, blockHeight));

    [Theory]
    [InlineData(140, 100, 100)]
    [InlineData(160, 100, 200)]
    [InlineData(150, 100, 200)]  // .5 rounds to even (banker's) — 1.5 -> 2
    public void SnapToBlock_RoundsToNearestBoundary(double offset, float blockHeight, double expected)
        => Assert.Equal(expected, MonthGridGeometry.SnapToBlock(offset, blockHeight));

    [Fact]
    public void ClampOffset_StopsAtFirstBlock()
        => Assert.Equal(0, MonthGridGeometry.ClampOffset(-500, 600, 600, 12));

    [Fact]
    public void ClampOffset_LastBlockRestsAgainstBottom()
    {
        // 12 blocks of 600, viewport 600 -> max offset = 12*600 - 600 = 6600.
        Assert.Equal(6600, MonthGridGeometry.ClampOffset(999999, 600, 600, 12));
    }

    [Fact]
    public void ClampOffset_ShortRangePinsToTop()
    {
        // One block shorter than the viewport can never scroll: max clamps to 0, not negative.
        Assert.Equal(0, MonthGridGeometry.ClampOffset(200, 800, 600, 1));
    }

    [Theory]
    // January 2026 starts on a Thursday; week starting Sunday -> 4 leading blanks.
    [InlineData(2026, 1, DayOfWeek.Sunday, 4)]
    [InlineData(2026, 1, DayOfWeek.Monday, 3)]
    // February 2026 starts on a Sunday.
    [InlineData(2026, 2, DayOfWeek.Sunday, 0)]
    [InlineData(2026, 2, DayOfWeek.Monday, 6)]
    public void FirstDayOffset_CountsLeadingBlanks(int year, int month, DayOfWeek firstDay, int expected)
        => Assert.Equal(expected, MonthGridGeometry.FirstDayOffset(year, month, firstDay));

    [Theory]
    [InlineData(2026, 2, DayOfWeek.Sunday, 4)]   // 28 days starting Sunday -> exactly 4 rows
    [InlineData(2026, 1, DayOfWeek.Sunday, 5)]   // 31 days, 4 leading blanks -> 5 rows
    [InlineData(2026, 8, DayOfWeek.Monday, 6)]   // 31 days, starts Saturday under Monday-first -> 6 rows
    public void WeekRows_SpansEnoughRows(int year, int month, DayOfWeek firstDay, int expected)
        => Assert.Equal(expected, MonthGridGeometry.WeekRows(year, month, firstDay));

    [Theory]
    [InlineData(2020, 1, 2020, 0)]
    [InlineData(2020, 12, 2020, 11)]
    [InlineData(2021, 1, 2020, 12)]
    [InlineData(2026, 7, 2020, 78)]
    public void BlockIndex_IsMonthsSinceMinYearJanuary(int year, int month, int minYear, int expected)
        => Assert.Equal(expected, MonthGridGeometry.BlockIndex(year, month, minYear));

    [Fact]
    public void DayCells_HasOneCellPerDayInOrder()
    {
        var cells = MonthGridGeometry.DayCells(0, 0, 280, 320, 2026, 2, DayOfWeek.Sunday);

        Assert.Equal(28, cells.Count); // February 2026
        Assert.Equal(new DateOnly(2026, 2, 1), cells[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 28), cells[^1].Date);
        for (int i = 0; i < cells.Count; i++)
        {
            Assert.Equal(i + 1, cells[i].Date.Day);
        }
    }

    [Fact]
    public void DayCells_CellSizeIsASeventhOfTheGrid()
    {
        var cells = MonthGridGeometry.DayCells(0, 0, 280, 350, 2026, 1, DayOfWeek.Sunday);

        Assert.All(cells, c => Assert.Equal(40f, c.Width));   // 280 / 7
        Assert.All(cells, c => Assert.Equal(50f, c.Height));  // 350 / 7
    }

    [Fact]
    public void DayCells_FirstDaySitsAtItsWeekdayColumnOnRowOne()
    {
        // Jan 2026 starts Thursday; Sunday-first -> 4 leading blanks, so day 1 is column 4.
        var cells = MonthGridGeometry.DayCells(0, 0, 280, 350, 2026, 1, DayOfWeek.Sunday);
        var day1 = cells[0];

        float cellW = 280f / 7f;
        float rowH = 350f / 7f;
        Assert.Equal(4 * cellW, day1.X);      // column 4
        Assert.Equal(rowH, day1.Y);           // row 1 (row 0 is the weekday heading)
    }

    [Fact]
    public void DayCells_HonorsGridOriginOffset()
    {
        var atOrigin = MonthGridGeometry.DayCells(0, 0, 140, 350, 2026, 3, DayOfWeek.Monday);
        var shifted = MonthGridGeometry.DayCells(16, 60, 140, 350, 2026, 3, DayOfWeek.Monday);

        Assert.Equal(atOrigin[0].X + 16, shifted[0].X);
        Assert.Equal(atOrigin[0].Y + 60, shifted[0].Y);
    }

    [Fact]
    public void DayCells_ConsecutiveDaysAdvanceOneColumnThenWrapDownAWeek()
    {
        var cells = MonthGridGeometry.DayCells(0, 0, 280, 350, 2026, 2, DayOfWeek.Sunday);
        float cellW = 280f / 7f;
        float rowH = 350f / 7f;

        // Feb 2026 starts Sunday (column 0). Day 7 ends the first week; day 8 wraps to column 0, next row.
        Assert.Equal(6 * cellW, cells[6].X);          // day 7 -> column 6
        Assert.Equal(cells[0].Y, cells[6].Y);          // ...same row as day 1
        Assert.Equal(0f, cells[7].X);                  // day 8 -> column 0
        Assert.Equal(cells[0].Y + rowH, cells[7].Y);   // ...one row down
    }

    [Fact]
    public void DayCells_DayReachedByTapMatchesLayout()
    {
        // Simulates the views' hit-test: find which cell contains a point.
        var cells = MonthGridGeometry.DayCells(0, 0, 280, 350, 2026, 1, DayOfWeek.Sunday);
        var target = cells.First(c => c.Date.Day == 15);
        float px = target.X + (target.Width / 2);
        float py = target.Y + (target.Height / 2);

        var hit = cells.First(c => px >= c.X && px < c.X + c.Width && py >= c.Y && py < c.Y + c.Height);

        Assert.Equal(new DateOnly(2026, 1, 15), hit.Date);
    }
}
