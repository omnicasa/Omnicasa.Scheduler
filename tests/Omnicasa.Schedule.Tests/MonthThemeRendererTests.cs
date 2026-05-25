using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class MonthThemeRendererTests
{
    private static readonly RectF Bounds = new RectF(0, 0, 280, 320);

    private static MonthDrawable May2026(ScheduleTheme theme, bool compact = false) => new MonthDrawable
    {
        Month = new DateOnly(2026, 5, 1),
        Today = null,
        Compact = compact,
        ShowHeader = false,
        Theme = theme,
    };

    [Fact]
    public void ScheduleTheme_FontDefaultsAreNull()
    {
        var theme = new ScheduleTheme();

        Assert.Null(theme.FontFamily);
        Assert.Null(theme.MonthHeaderFontSize);
        Assert.Null(theme.WeekdayFontSize);
        Assert.Null(theme.DayNumberFontSize);
    }

    [Fact]
    public void Draw_DayNumberFontSize_OverridesAutoSizing()
    {
        var canvas = new RecordingCanvas();
        var theme = new ScheduleTheme { DayNumberFontSize = 30 };

        May2026(theme).Draw(canvas, Bounds);

        // Every day cell paints at the themed size.
        Assert.Contains(30f, canvas.FontSizes);
    }

    [Fact]
    public void Draw_WeekdayFontSize_OverridesAutoSizing()
    {
        var canvas = new RecordingCanvas();
        var theme = new ScheduleTheme { WeekdayFontSize = 17 };

        May2026(theme).Draw(canvas, Bounds);

        Assert.Contains(17f, canvas.FontSizes);
    }

    [Fact]
    public void Draw_NonCompact_UsesLargerDayFontThanCompact()
    {
        var compact = new RecordingCanvas();
        var full = new RecordingCanvas();

        May2026(new ScheduleTheme(), compact: true).Draw(compact, Bounds);
        May2026(new ScheduleTheme(), compact: false).Draw(full, Bounds);

        // Auto-fit caps differ: full-size months use a larger day font than the compact year grid.
        Assert.True(full.FontSizes.Max() > compact.FontSizes.Max());
    }

    [Fact]
    public void Draw_TodayHighlight_UsesThemeTodayColor()
    {
        var canvas = new RecordingCanvas();
        var todayColor = Color.FromArgb("#123456");
        var theme = new ScheduleTheme { Today = todayColor };
        var drawable = May2026(theme);
        drawable.Today = new DateOnly(2026, 5, 15);

        drawable.Draw(canvas, Bounds);

        Assert.Contains(todayColor, canvas.FillColors);
    }

    [Fact]
    public void Draw_CustomRenderer_CanReadThemeAndDateFromContext()
    {
        var theme = new ScheduleTheme();
        var renderer = new CapturingRenderer();
        var drawable = May2026(theme);
        drawable.Renderer = renderer;

        drawable.Draw(new RecordingCanvas(), Bounds);

        Assert.Equal(31, renderer.Days.Count);
        Assert.Same(theme, renderer.LastTheme);
        Assert.Contains(new DateOnly(2026, 5, 1), renderer.Days);
        Assert.Contains(new DateOnly(2026, 5, 31), renderer.Days);
    }

    [Fact]
    public void Draw_CustomHeaderRenderer_IsInvokedWhenHeaderShown()
    {
        var renderer = new CapturingRenderer();
        var drawable = May2026(new ScheduleTheme());
        drawable.ShowHeader = true;
        drawable.Renderer = renderer;

        drawable.Draw(new RecordingCanvas(), Bounds);

        Assert.Equal(1, renderer.HeaderCalls);
    }

    private sealed class CapturingRenderer : MonthRenderer
    {
        public List<DateOnly> Days { get; } = new List<DateOnly>();

        public ScheduleTheme? LastTheme { get; private set; }

        public int HeaderCalls { get; private set; }

        public override void DrawDay(MonthDayContext ctx)
        {
            Days.Add(ctx.Date);
            LastTheme = ctx.Theme;
        }

        public override void DrawHeader(MonthHeaderContext ctx) => HeaderCalls++;
    }
}
