using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleOffHoursTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static ScheduleRenderContext Ctx(bool shading) =>
        new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            Scale = new TimeScale(60),
            TimeRailWidth = 56,
            WorkDayStart = TimeSpan.FromHours(8),
            WorkDayEnd = TimeSpan.FromHours(18),
            ShowOffHoursShading = shading,
            Columns = new[] { new ScheduleViewColumn { DayStart = Day } },
        };

    [Fact]
    public void DrawOffHours_ShadingOn_FillsTwoBandsWithCorrectY()
    {
        var canvas = new RecordingCanvas();
        var ctx = Ctx(shading: true);

        new ScheduleViewRenderer().DrawOffHours(canvas, contentX: 56, colW: 100, ctx);

        // 00:00→08:00 top band and 18:00→24:00 bottom band, at 60px/hour.
        Assert.Equal(2, canvas.FilledRectangles.Count);
        Assert.Contains(new RectF(56, 0, 100, 480), canvas.FilledRectangles);
        Assert.Contains(new RectF(56, 1080, 100, 360), canvas.FilledRectangles);
        Assert.Equal(ctx.Theme.OffHoursShade, canvas.LastFillColor);
    }

    [Fact]
    public void DrawOffHours_ShadingOff_FillsNothing()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawOffHours(canvas, contentX: 56, colW: 100, Ctx(shading: false));

        Assert.Empty(canvas.FilledRectangles);
        Assert.Empty(canvas.Ops);
    }

    [Fact]
    public void DrawOffHours_MultipleColumns_ShadesEachColumn()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            Scale = new TimeScale(60),
            WorkDayStart = TimeSpan.FromHours(9),
            WorkDayEnd = TimeSpan.FromHours(17),
            ShowOffHoursShading = true,
            Columns = new[]
            {
                new ScheduleViewColumn { DayStart = Day },
                new ScheduleViewColumn { DayStart = Day.AddDays(1) },
                new ScheduleViewColumn { DayStart = Day.AddDays(2) },
            },
        };

        new ScheduleViewRenderer().DrawOffHours(canvas, contentX: 56, colW: 80, ctx);

        // Two bands per column across three columns.
        Assert.Equal(6, canvas.FilledRectangles.Count);
        Assert.Contains(new RectF(56, 0, 80, 540), canvas.FilledRectangles);        // col 0 top (0→9h)
        Assert.Contains(new RectF(216, 1020, 80, 420), canvas.FilledRectangles);    // col 2 bottom (17→24h)
    }
}
