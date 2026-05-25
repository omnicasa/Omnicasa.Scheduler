using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class MonthDrawableTests
{
    private static readonly RectF Bounds = new RectF(0, 0, 280, 320);

    private static MonthDrawable May2026(bool compact = false) => new MonthDrawable
    {
        Month = new DateOnly(2026, 5, 1),
        Today = null,
        Compact = compact,
        ShowHeader = false,
        Theme = new ScheduleTheme(),
    };

    [Fact]
    public void Draw_PopulatesHitMapWithEveryDay()
    {
        var drawable = May2026();

        drawable.Draw(new RecordingCanvas(), Bounds);

        Assert.Equal(31, drawable.HitMap.Count);                 // May has 31 days
        Assert.Contains(new DateOnly(2026, 5, 1), drawable.HitMap.Keys);
        Assert.Contains(new DateOnly(2026, 5, 31), drawable.HitMap.Keys);
        Assert.All(drawable.HitMap.Values, r => Assert.True(r.Width > 0 && r.Height > 0));
    }

    [Fact]
    public void Draw_RendersEveryDayNumber()
    {
        var canvas = new RecordingCanvas();

        May2026().Draw(canvas, Bounds);

        for (int d = 1; d <= 31; d++)
        {
            Assert.Contains(d.ToString(System.Globalization.CultureInfo.CurrentCulture), canvas.Strings);
        }
    }

    [Fact]
    public void Draw_WithHeader_DrawsMonthTitle()
    {
        var canvas = new RecordingCanvas();
        var drawable = May2026();
        drawable.ShowHeader = true;

        drawable.Draw(canvas, Bounds);

        // Abbreviated month name for May.
        var expected = new DateTime(2026, 5, 1).ToString("MMM", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Contains(expected, canvas.Strings);
    }

    [Fact]
    public void Draw_TodayInMonth_DrawsHighlightCircle()
    {
        var canvas = new RecordingCanvas();
        var drawable = May2026();
        drawable.Today = new DateOnly(2026, 5, 15);

        drawable.Draw(canvas, Bounds);

        // The "today" highlight is a filled circle (FillEllipse).
        Assert.NotEmpty(canvas.FilledEllipses);
    }

    [Fact]
    public void Draw_DensityDot_AppearsOnlyForDatesWithEvents()
    {
        var withEvents = new RecordingCanvas();
        var drawable = May2026();
        drawable.CountProvider = d => d == new DateOnly(2026, 5, 10) ? 3 : 0;

        drawable.Draw(withEvents, Bounds);

        var withoutEvents = new RecordingCanvas();
        var drawable2 = May2026();
        drawable2.CountProvider = _ => 0;
        drawable2.Draw(withoutEvents, Bounds);

        // The single event day adds exactly one extra filled circle (the density dot).
        Assert.Equal(withoutEvents.FilledEllipses.Count + 1, withEvents.FilledEllipses.Count);
    }

    [Fact]
    public void Draw_CustomRenderer_IsInvokedPerDay()
    {
        var canvas = new RecordingCanvas();
        var renderer = new CountingRenderer();
        var drawable = May2026();
        drawable.Renderer = renderer;

        drawable.Draw(canvas, Bounds);

        Assert.Equal(31, renderer.DayCalls);
        Assert.Equal(7, renderer.WeekdayCalls);
        // Override drew nothing, proving the default day painting was bypassed.
        Assert.Empty(canvas.Strings);
    }

    private sealed class CountingRenderer : MonthRenderer
    {
        public int DayCalls { get; private set; }

        public int WeekdayCalls { get; private set; }

        public override void DrawDay(MonthDayContext ctx) => DayCalls++;

        public override void DrawWeekday(MonthWeekdayContext ctx) => WeekdayCalls++;
    }
}
