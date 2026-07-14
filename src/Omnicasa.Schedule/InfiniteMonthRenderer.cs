#if ANDROID || IOS
using System.Globalization;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Omnicasa.Schedule;

/// <summary>Paints the month title for <see cref="InfiniteMonthRenderer"/>.</summary>
public readonly struct MonthTitlePaintContext
{
    /// <summary>Target GPU canvas (drawing in logical units).</summary>
    public SKCanvas Canvas { get; init; }

    /// <summary>Active theme (colors + optional font family).</summary>
    public ScheduleTheme Theme { get; init; }

    /// <summary>Box the title occupies.</summary>
    public SKRect Rect { get; init; }

    /// <summary>Title text (e.g. "March 2026").</summary>
    public string Text { get; init; }

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }
}

/// <summary>Paints one weekday-letter heading cell for <see cref="InfiniteMonthRenderer"/>.</summary>
public readonly struct MonthWeekdayPaintContext
{
    /// <summary>Target GPU canvas.</summary>
    public SKCanvas Canvas { get; init; }

    /// <summary>Active theme.</summary>
    public ScheduleTheme Theme { get; init; }

    /// <summary>Cell the weekday letter occupies.</summary>
    public SKRect Rect { get; init; }

    /// <summary>Weekday letter to draw.</summary>
    public string Text { get; init; }

    /// <summary>Which weekday this cell represents.</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }
}

/// <summary>Paints one day cell (number, today highlight, density dot) for <see cref="InfiniteMonthRenderer"/>.</summary>
public readonly struct MonthDayPaintContext
{
    /// <summary>Target GPU canvas.</summary>
    public SKCanvas Canvas { get; init; }

    /// <summary>Active theme.</summary>
    public ScheduleTheme Theme { get; init; }

    /// <summary>Cell the day occupies.</summary>
    public SKRect Rect { get; init; }

    /// <summary>The date this cell represents.</summary>
    public DateOnly Date { get; init; }

    /// <summary>True when this is the highlighted "today" cell.</summary>
    public bool IsToday { get; init; }

    /// <summary>Number of events on this date (drives the density dot).</summary>
    public int EventCount { get; init; }

    /// <summary>Resolved text color (today / weekend / weekday already applied).</summary>
    public SKColor TextColor { get; init; }

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }

    /// <summary>Whether the compact (year-grid) sizing is in effect; affects dot size.</summary>
    public bool Compact { get; init; }
}

/// <summary>
/// Pluggable Skia painter for <see cref="InfiniteMonthCalendarView"/> — the GPU-canvas counterpart of
/// <c>MonthRenderer</c>. Subclass and override only the primitives you need; each default reproduces
/// the built-in look. The most common override is <see cref="DrawDay"/> (custom day cells). Layout
/// and hit-testing stay in the view.
/// </summary>
public class InfiniteMonthRenderer
{
    private static readonly SKTypeface BoldTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default;

    private static readonly SKTypeface RegularTypeface = SKTypeface.Default;

    /// <summary>Shared default renderer used when none is supplied.</summary>
    public static InfiniteMonthRenderer Default { get; } = new InfiniteMonthRenderer();

    /// <summary>Draws the month title. Default: left-aligned, vertically centered, bold accent.</summary>
    /// <param name="ctx">Geometry, theme, text, and font size for the title.</param>
    public virtual void DrawTitle(MonthTitlePaintContext ctx)
    {
        using var paint = new SKPaint { Color = ToSk(ctx.Theme.Accent), IsAntialias = true };
        using var font = new SKFont(ResolveTypeface(ctx.Theme, bold: true), ctx.FontSize);
        DrawText(ctx.Canvas, ctx.Text, ctx.Rect, font, paint, SKTextAlign.Left);
    }

    /// <summary>Draws one weekday-letter heading. Default: centered, muted.</summary>
    /// <param name="ctx">Geometry, theme, letter, and font size for the heading.</param>
    public virtual void DrawWeekday(MonthWeekdayPaintContext ctx)
    {
        using var paint = new SKPaint { Color = ToSk(ctx.Theme.Muted), IsAntialias = true };
        using var font = new SKFont(ResolveTypeface(ctx.Theme, bold: false), ctx.FontSize);
        DrawText(ctx.Canvas, ctx.Text, ctx.Rect, font, paint, SKTextAlign.Center);
    }

    /// <summary>
    /// Draws one day cell. Default reproduces the built-in look: a filled "today" circle, the day
    /// number centered, and a small density dot when the day has events. Override and switch on
    /// <see cref="MonthDayPaintContext.Date"/> for custom looks.
    /// </summary>
    /// <param name="ctx">Geometry, theme, date, and resolved colors for the cell.</param>
    public virtual void DrawDay(MonthDayPaintContext ctx)
    {
        var canvas = ctx.Canvas;
        var rect = ctx.Rect;

        if (ctx.IsToday)
        {
            float r = Math.Min(rect.Width, rect.Height) * 0.38f;
            using var todayFill = new SKPaint { Color = ToSk(ctx.Theme.Today), IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawCircle(rect.MidX, rect.MidY, r, todayFill);
        }

        using var text = new SKPaint { Color = ctx.TextColor, IsAntialias = true };
        using var font = new SKFont(ResolveTypeface(ctx.Theme, bold: ctx.IsToday), ctx.FontSize);
        DrawText(canvas, ctx.Date.Day.ToString(CultureInfo.CurrentCulture), rect, font, text, SKTextAlign.Center);

        if (ctx.EventCount > 0)
        {
            using var dot = new SKPaint
            {
                Color = ctx.IsToday ? SKColors.White : ToSk(ctx.Theme.Accent),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            float dotY = rect.Bottom - (ctx.Compact ? 3f : 7f);
            canvas.DrawCircle(rect.MidX, dotY, ctx.Compact ? 1.4f : 3f, dot);
        }
    }

    /// <summary>Resolves the typeface for the theme's font family (falling back to the platform default).</summary>
    /// <param name="theme">Active theme (its <see cref="ScheduleTheme.FontFamily"/> is honored when set).</param>
    /// <param name="bold">Whether to request the bold weight.</param>
    protected static SKTypeface ResolveTypeface(ScheduleTheme theme, bool bold)
    {
        if (!string.IsNullOrEmpty(theme.FontFamily))
        {
            var named = SKTypeface.FromFamilyName(theme.FontFamily, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            if (named is not null)
            {
                return named;
            }
        }

        return bold ? BoldTypeface : RegularTypeface;
    }

    /// <summary>Draws text within <paramref name="rect"/>, vertically centered, horizontally per <paramref name="align"/>.</summary>
    /// <param name="canvas">Target canvas.</param>
    /// <param name="text">Text to draw.</param>
    /// <param name="rect">Box to place the text in.</param>
    /// <param name="font">Resolved font.</param>
    /// <param name="paint">Resolved paint (color).</param>
    /// <param name="align">Horizontal alignment: Left anchors to the rect's left, Center to its middle.</param>
    protected static void DrawText(SKCanvas canvas, string text, SKRect rect, SKFont font, SKPaint paint, SKTextAlign align)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var metrics = font.Metrics;
        float baseline = rect.MidY - ((metrics.Ascent + metrics.Descent) / 2f);
        float x = align == SKTextAlign.Left ? rect.Left : rect.MidX;
        canvas.DrawText(text, x, baseline, align, font, paint);
    }

    /// <summary>Converts a MAUI <see cref="Color"/> to an <see cref="SKColor"/>.</summary>
    /// <param name="color">Source color (null treated as transparent).</param>
    protected static SKColor ToSk(Color? color)
    {
        var c = color ?? Colors.Transparent;
        return new SKColor(
            (byte)Math.Round(c.Red * 255),
            (byte)Math.Round(c.Green * 255),
            (byte)Math.Round(c.Blue * 255),
            (byte)Math.Round(c.Alpha * 255));
    }
}
#endif
