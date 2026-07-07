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
    public void Draw_CustomHeaderBackground_ReplacesDefaultFill()
    {
        var canvas = new RecordingCanvas();
        var renderer = new HalfTintHeaderRenderer();
        var drawable = new ScheduleHeaderDrawable { Context = Context(), Renderer = renderer };

        drawable.Draw(canvas, new RectF(0, 0, 400, 48));

        Assert.True(renderer.BackgroundDrawn);
        Assert.DoesNotContain(new RectF(0, 0, 400, 48), canvas.FilledRectangles); // default full fill replaced
        Assert.Contains(new RectF(0, 0, 400, 24), canvas.FilledRectangles);
        Assert.Contains("MON", canvas.Strings);
    }

    [Fact]
    public void Draw_TransparentMode_SkipsCustomHeaderBackgroundToo()
    {
        var canvas = new RecordingCanvas();
        var renderer = new HalfTintHeaderRenderer();
        var drawable = new ScheduleHeaderDrawable { Context = Context(), Renderer = renderer, DrawsBackground = false };

        drawable.Draw(canvas, new RectF(0, 0, 400, 48));

        Assert.False(renderer.BackgroundDrawn);
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

    // Paints only the top half so tests can tell it apart from the default full-rect fill.
    private sealed class HalfTintHeaderRenderer : ScheduleViewRenderer
    {
        public bool BackgroundDrawn { get; private set; }

        public override void DrawHeaderBackground(ICanvas canvas, RectF dirtyRect, ScheduleRenderContext ctx)
        {
            BackgroundDrawn = true;
            canvas.FillColor = Colors.Red;
            canvas.FillRectangle(0, 0, dirtyRect.Width, dirtyRect.Height / 2);
        }
    }
}
