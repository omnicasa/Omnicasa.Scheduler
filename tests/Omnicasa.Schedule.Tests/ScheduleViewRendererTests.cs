using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleViewRendererTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static ScheduleAppointmentContext ApptCtx(RecordingCanvas canvas, RectF rect, string title) =>
        new ScheduleAppointmentContext
        {
            Canvas = canvas,
            Item = new TestScheduleItem
            {
                Title = title,
                Start = Day.AddHours(9),
                End = Day.AddHours(10),
            },
            Rect = rect,
            BlockColor = Colors.DodgerBlue,
            Theme = new ScheduleViewTheme(),
        };

    [Fact]
    public void Default_IsSingletonAndNotNull()
    {
        Assert.NotNull(ScheduleViewRenderer.Default);
        Assert.Same(ScheduleViewRenderer.Default, ScheduleViewRenderer.Default);
    }

    [Fact]
    public void DrawBackground_FillsFullDirtyRect()
    {
        var canvas = new RecordingCanvas();
        var theme = new ScheduleViewTheme();

        new ScheduleViewRenderer().DrawBackground(canvas, new RectF(0, 0, 320, 480), theme);

        Assert.Contains(new RectF(0, 0, 320, 480), canvas.FilledRectangles);
        Assert.Equal(theme.Background, canvas.LastFillColor);
    }

    [Fact]
    public void DrawAppointment_Default_PaintsBlockAndTitle()
    {
        var canvas = new RecordingCanvas();
        var rect = new RectF(10, 20, 100, 80);

        new ScheduleViewRenderer().DrawAppointment(ApptCtx(canvas, rect, "Standup"));

        // Soft body fill + accent strip => at least two rounded rectangles, one matching the block rect.
        Assert.True(canvas.FilledRoundedRectangles.Count >= 2);
        Assert.Contains(rect, canvas.FilledRoundedRectangles);
        Assert.Contains("Standup", canvas.Strings);
    }

    [Fact]
    public void DrawAppointment_TallBlock_AlsoDrawsTimeRange()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawAppointment(ApptCtx(canvas, new RectF(0, 0, 120, 200), "Long"));

        // Title + a time-range line.
        Assert.True(canvas.Strings.Count >= 2);
    }

    [Fact]
    public void DrawAppointment_ShortBlock_OmitsTimeRange()
    {
        var canvas = new RecordingCanvas();

        // Height below the title+range threshold: only the title is drawn.
        new ScheduleViewRenderer().DrawAppointment(ApptCtx(canvas, new RectF(0, 0, 120, 16), "Tiny"));

        Assert.Single(canvas.Strings);
        Assert.Equal("Tiny", canvas.Strings[0]);
    }

    [Fact]
    public void DrawAppointment_Override_IsInvokedInsteadOfBase()
    {
        var canvas = new RecordingCanvas();
        var renderer = new MarkerRenderer();

        renderer.DrawAppointment(ApptCtx(canvas, new RectF(0, 0, 100, 100), "X"));

        Assert.True(renderer.Called);
        // The override drew nothing on the canvas, proving base was not used.
        Assert.Empty(canvas.FilledRoundedRectangles);
    }

    [Fact]
    public void DrawHourGrid_Draws25HourLinesAnd24Labels()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            Scale = new TimeScale(60),
            TimeRailWidth = 56,
        };

        new ScheduleViewRenderer().DrawHourGrid(canvas, 320, 56, ctx);

        Assert.Equal(25, canvas.DrawLineCount);                 // 0..24 inclusive
        Assert.Equal(24, canvas.Strings.Count(s => s.Contains("AM") || s.Contains("PM")));
    }

    [Fact]
    public void DrawHourGrid_CustomHourLabelFormat_DrawsFormattedLabels()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme { HourLabelFormat = "H" },
            Scale = new TimeScale(60),
            TimeRailWidth = 56,
        };

        new ScheduleViewRenderer().DrawHourGrid(canvas, 320, 56, ctx);

        Assert.Contains("23", canvas.Strings);
        Assert.DoesNotContain(canvas.Strings, s => s.Contains("AM") || s.Contains("PM"));
    }

    [Fact]
    public void DrawColumnSeparators_DrawsOneLinePerInteriorBoundary()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawColumnSeparators(canvas, 56, 50, n: 3, h: 400, new ScheduleViewTheme());

        Assert.Equal(2, canvas.DrawLineCount);                  // n-1 separators
    }

    [Fact]
    public void DrawColumnSeparators_SingleColumn_DrawsNothing()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawColumnSeparators(canvas, 56, 50, n: 1, h: 400, new ScheduleViewTheme());

        Assert.Equal(0, canvas.DrawLineCount);
    }

    [Fact]
    public void DrawTodayMarker_NoNow_DrawsNothing()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            Scale = new TimeScale(60),
            Now = null,
            Columns = new[] { new ScheduleViewColumn { DayStart = Day } },
        };

        new ScheduleViewRenderer().DrawTodayMarker(canvas, 56, 100, ctx);

        Assert.Empty(canvas.Ops);
    }

    [Fact]
    public void DrawTodayMarker_MarksMatchingTodayColumn()
    {
        var now = Day.AddHours(10);
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            Scale = new TimeScale(60),
            Now = now,
            Columns = new[]
            {
                new ScheduleViewColumn { DayStart = Day },               // today
                new ScheduleViewColumn { DayStart = Day.AddDays(1) },    // not today
            },
        };

        new ScheduleViewRenderer().DrawTodayMarker(canvas, 56, 100, ctx);

        // One column matches "today": a dot (FillEllipse) + a line.
        Assert.Single(canvas.FilledEllipses);
        Assert.Equal(1, canvas.DrawLineCount);
    }

    [Fact]
    public void DrawTypingItem_DrawsShadowHandlesAndTitle()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleTypingContext
        {
            Canvas = canvas,
            Item = new TestTypingItem { Title = "Draft", Start = Day.AddHours(9), End = Day.AddHours(11) },
            Rect = new RectF(10, 10, 120, 160),
            BlockColor = Colors.DodgerBlue,
            Theme = new ScheduleViewTheme(),
        };

        new ScheduleViewRenderer().DrawTypingItem(ctx);

        Assert.Equal(1, canvas.ShadowCount);
        Assert.True(canvas.SaveStateCount >= 1 && canvas.RestoreStateCount >= 1);
        Assert.Equal(2, canvas.FilledEllipses.Count);           // two corner handles
        Assert.Contains("Draft", canvas.Strings);
    }

    private static ScheduleHoldingContext HoldingCtx(RecordingCanvas canvas, RectF rect) =>
        new ScheduleHoldingContext
        {
            Canvas = canvas,
            Item = new TestScheduleItem { Title = "Held", Start = Day.AddHours(9), End = Day.AddHours(11) },
            Rect = rect,
            DisplayStart = Day.AddHours(9),
            DisplayEnd = Day.AddHours(11),
            BlockColor = Colors.DodgerBlue,
            Theme = new ScheduleViewTheme(),
        };

    [Fact]
    public void DrawHoldingItem_DrawsShadowBlockResizeHandlesAndTitle()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawHoldingItem(HoldingCtx(canvas, new RectF(10, 10, 120, 160)));

        Assert.Equal(1, canvas.ShadowCount);
        Assert.True(canvas.SaveStateCount >= 1 && canvas.RestoreStateCount >= 1);
        Assert.True(canvas.FilledRoundedRectangles.Count >= 1);   // the block
        Assert.Equal(2, canvas.FilledEllipses.Count);             // start + end resize handles
        Assert.Contains("Held", canvas.Strings);
    }

    [Fact]
    public void DrawHoldingItem_TallBlock_AlsoDrawsTimeRange()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawHoldingItem(HoldingCtx(canvas, new RectF(0, 0, 120, 200)));

        // Title + a time-range line.
        Assert.True(canvas.Strings.Count >= 2);
    }

    [Fact]
    public void DrawHoldingItem_Override_IsInvokedInsteadOfBase()
    {
        var canvas = new RecordingCanvas();
        var renderer = new HoldingMarkerRenderer();

        renderer.DrawHoldingItem(HoldingCtx(canvas, new RectF(0, 0, 120, 160)));

        Assert.True(renderer.Called);
        Assert.Empty(canvas.FilledEllipses);   // override drew nothing → base bypassed
        Assert.Empty(canvas.Strings);
    }

    [Fact]
    public void DrawHeader_DrawsOnePrimaryPerDayGroupAndOneSecondaryPerColumn()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            TimeRailWidth = 56,
            HeaderHeight = 48,
            Columns = new[]
            {
                new ScheduleViewColumn { DayStart = Day, HeaderPrimary = "MON 25", HeaderSecondary = "25" },
                new ScheduleViewColumn { DayStart = Day, HeaderPrimary = "MON 25", HeaderSecondary = "A" },
                new ScheduleViewColumn { DayStart = Day.AddDays(1), HeaderPrimary = "TUE 26", HeaderSecondary = "26" },
            },
        };

        new ScheduleViewRenderer().DrawHeader(canvas, new RectF(0, 0, 320, 48), ctx);

        // Two day-groups => "MON 25" once, "TUE 26" once. Three non-empty secondaries.
        Assert.Equal(1, canvas.Strings.Count(s => s == "MON 25"));
        Assert.Equal(1, canvas.Strings.Count(s => s == "TUE 26"));
        Assert.Equal(2 + 3, canvas.Strings.Count);
    }

    [Fact]
    public void DrawHeader_ZeroHeight_DrawsNothing()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext
        {
            Theme = new ScheduleViewTheme(),
            TimeRailWidth = 56,
            HeaderHeight = 0,
            Columns = new[] { new ScheduleViewColumn { DayStart = Day, HeaderPrimary = "MON 25" } },
        };

        new ScheduleViewRenderer().DrawHeader(canvas, new RectF(0, 0, 320, 0), ctx);

        Assert.Empty(canvas.Strings);
    }

    [Fact]
    public void DrawHeader_NoColumns_DrawsNothing()
    {
        var canvas = new RecordingCanvas();
        var ctx = new ScheduleRenderContext { Theme = new ScheduleViewTheme(), HeaderHeight = 48 };

        new ScheduleViewRenderer().DrawHeader(canvas, new RectF(0, 0, 320, 48), ctx);

        Assert.Empty(canvas.Strings);
    }

    private sealed class MarkerRenderer : ScheduleViewRenderer
    {
        public bool Called { get; private set; }

        public override void DrawAppointment(ScheduleAppointmentContext ctx) => Called = true;
    }

    private sealed class HoldingMarkerRenderer : ScheduleViewRenderer
    {
        public bool Called { get; private set; }

        public override void DrawHoldingItem(ScheduleHoldingContext ctx) => Called = true;
    }
}
