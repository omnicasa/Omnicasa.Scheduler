using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleHeaderDrawableTests
{
    // A Monday.
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static ScheduleRenderContext Context() => new ScheduleRenderContext
    {
        Columns = ScheduleColumnBuilder.Build(Day, Day.AddDays(6), 7, persons: null),
        HeaderHeight = 48,
        TimeRailWidth = 56,
    };

    [Fact]
    public void Draw_Default_PaintsOpaqueBackground()
    {
        var canvas = new RecordingCanvas();
        var drawable = new ScheduleHeaderDrawable { Context = Context() };

        drawable.Draw(canvas, new RectF(0, 0, 400, 48));

        Assert.Contains(new RectF(0, 0, 400, 48), canvas.FilledRectangles);
        Assert.Contains("MON", canvas.Strings);
    }

    [Fact]
    public void Draw_TransparentMode_SkipsBackgroundButDrawsHeader()
    {
        var canvas = new RecordingCanvas();
        var drawable = new ScheduleHeaderDrawable { Context = Context(), DrawsBackground = false };

        drawable.Draw(canvas, new RectF(0, 0, 400, 48));

        Assert.DoesNotContain(new RectF(0, 0, 400, 48), canvas.FilledRectangles);
        Assert.Contains("MON", canvas.Strings);
    }

    [Fact]
    public void Draw_SingleDayColumns_StillRendersWhenHeaderHeightSet()
    {
        // A linked/standalone header shows single-day columns the in-house bar would collapse.
        var canvas = new RecordingCanvas();
        var drawable = new ScheduleHeaderDrawable
        {
            Context = new ScheduleRenderContext
            {
                Columns = ScheduleColumnBuilder.Build(Day, Day, 1, persons: null),
                HeaderHeight = 48,
                TimeRailWidth = 56,
            },
        };

        drawable.Draw(canvas, new RectF(0, 0, 400, 48));

        Assert.Contains("MON", canvas.Strings);
    }
}
