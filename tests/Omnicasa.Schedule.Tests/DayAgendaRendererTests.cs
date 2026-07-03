using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class DayAgendaRendererTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static DayAgendaAppointmentContext ApptCtx(
        RecordingCanvas canvas,
        RectF rect,
        bool isGhost = false,
        bool showResizeHandle = false) =>
        new DayAgendaAppointmentContext
        {
            Canvas = canvas,
            Item = new Appointment { Title = "Sync", Start = Day.AddHours(9), End = Day.AddHours(10) },
            Rect = rect,
            BlockColor = Colors.MediumSeaGreen,
            Theme = new ScheduleTheme(),
            FontScale = 1f,
            DisplayStart = Day.AddHours(9),
            DisplayEnd = Day.AddHours(10),
            IsGhost = isGhost,
            ShowResizeHandle = showResizeHandle,
        };

    [Fact]
    public void Default_IsSingletonAndNotNull()
    {
        Assert.NotNull(DayAgendaRenderer.Default);
        Assert.Same(DayAgendaRenderer.Default, DayAgendaRenderer.Default);
    }

    [Fact]
    public void DrawBackground_FillsFullDirtyRect()
    {
        var canvas = new RecordingCanvas();
        var theme = new ScheduleTheme();

        new DayAgendaRenderer().DrawBackground(canvas, new RectF(0, 0, 200, 300), theme);

        Assert.Contains(new RectF(0, 0, 200, 300), canvas.FilledRectangles);
        Assert.Equal(theme.Background, canvas.LastFillColor);
    }

    [Fact]
    public void DrawAppointment_Default_PaintsBlockAndTitle()
    {
        var canvas = new RecordingCanvas();
        var rect = new RectF(5, 5, 100, 90);

        new DayAgendaRenderer().DrawAppointment(ApptCtx(canvas, rect));

        Assert.True(canvas.FilledRoundedRectangles.Count >= 2);
        Assert.Contains(rect, canvas.FilledRoundedRectangles);
        Assert.Contains("Sync", canvas.Strings);
    }

    [Fact]
    public void DrawAppointment_WithResizeHandle_DrawsExtraRoundedRect()
    {
        var canvasNoHandle = new RecordingCanvas();
        var canvasHandle = new RecordingCanvas();
        var rect = new RectF(0, 0, 100, 90);

        new DayAgendaRenderer().DrawAppointment(ApptCtx(canvasNoHandle, rect, showResizeHandle: false));
        new DayAgendaRenderer().DrawAppointment(ApptCtx(canvasHandle, rect, showResizeHandle: true));

        // The handle pill is one extra rounded rectangle.
        Assert.Equal(canvasNoHandle.FilledRoundedRectangles.Count + 1, canvasHandle.FilledRoundedRectangles.Count);
    }

    [Fact]
    public void DrawAppointment_Override_IsInvokedInsteadOfBase()
    {
        var canvas = new RecordingCanvas();
        var renderer = new MarkerRenderer();

        renderer.DrawAppointment(ApptCtx(canvas, new RectF(0, 0, 80, 80)));

        Assert.True(renderer.Called);
        Assert.Empty(canvas.FilledRoundedRectangles);
    }

    [Fact]
    public void DrawHourGrid_Draws25Lines()
    {
        var canvas = new RecordingCanvas();

        new DayAgendaRenderer().DrawHourGrid(canvas, 200, 56, fontScale: 1f, new TimeScale(60), new ScheduleTheme(), 56);

        Assert.Equal(25, canvas.DrawLineCount);
    }

    [Fact]
    public void DrawHourGrid_CustomHourLabelFormat_DrawsFormattedLabels()
    {
        var canvas = new RecordingCanvas();
        var theme = new ScheduleTheme { HourLabelFormat = "HH" };

        new DayAgendaRenderer().DrawHourGrid(canvas, 200, 56, fontScale: 1f, new TimeScale(60), theme, 56);

        Assert.Contains("23", canvas.Strings);
        Assert.Contains("09", canvas.Strings);
        Assert.DoesNotContain(canvas.Strings, s => s.Contains("AM") || s.Contains("PM"));
    }

    [Fact]
    public void DrawColumnSeparators_HonorsColumnCount()
    {
        var canvas = new RecordingCanvas();

        new DayAgendaRenderer().DrawColumnSeparators(canvas, 56, 40, n: 4, h: 400, headerHeight: 40, new ScheduleTheme());

        Assert.Equal(3, canvas.DrawLineCount);
    }

    [Fact]
    public void DrawTodayMarker_MarksOnlyMatchingColumn()
    {
        var now = Day.AddHours(13);
        var canvas = new RecordingCanvas();
        var columns = new[]
        {
            new AgendaColumn { DayStart = Day.AddDays(-1) },
            new AgendaColumn { DayStart = Day },              // today
        };

        new DayAgendaRenderer().DrawTodayMarker(canvas, 56, 80, n: 2, columns, now, new TimeScale(60), new ScheduleTheme());

        Assert.Single(canvas.FilledEllipses);
        Assert.Equal(1, canvas.DrawLineCount);
    }

    private sealed class MarkerRenderer : DayAgendaRenderer
    {
        public bool Called { get; private set; }

        public override void DrawAppointment(DayAgendaAppointmentContext ctx) => Called = true;
    }
}
