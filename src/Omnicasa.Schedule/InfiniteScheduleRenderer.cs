#if ANDROID || IOS
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Omnicasa.Schedule;

/// <summary>
/// Everything a renderer needs to paint one block into <see cref="Rect"/> on the GPU canvas.
/// Carried by the <see cref="InfiniteScheduleRenderer"/> draw hooks.
/// </summary>
public readonly struct InfiniteScheduleBlock
{
    /// <summary>Rectangle the block occupies (already laid out and clipped to its column).</summary>
    public SKRect Rect { get; init; }

    /// <summary>Resolved block color (item color, else column accent, else theme accent).</summary>
    public SKColor Color { get; init; }

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; }

    /// <summary>Block title, if any.</summary>
    public string? Title { get; init; }

    /// <summary>Start time currently shown (drag position while dragging a holding block).</summary>
    public DateTime Start { get; init; }

    /// <summary>End time currently shown.</summary>
    public DateTime End { get; init; }

    /// <summary>True while a holding block is being dragged.</summary>
    public bool IsDragging { get; init; }

    /// <summary>The underlying appointment (set for appointments/holding; null for the typing draft).</summary>
    public IScheduleItem? Item { get; init; }
}

/// <summary>
/// One all-day / multi-day bar laid out in the all-day panel between the header and the time grid.
/// Carried by <see cref="InfiniteScheduleRenderer.DrawAllDayBar"/>.
/// </summary>
public readonly struct InfiniteAllDayBlock
{
    /// <summary>Rectangle the bar occupies, in canvas logical units (already clipped to the panel).</summary>
    public SKRect Rect { get; init; }

    /// <summary>Resolved bar color (item color, else theme accent).</summary>
    public SKColor Color { get; init; }

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; }

    /// <summary>Bar title, if any.</summary>
    public string? Title { get; init; }

    /// <summary>True when the item continues before the leftmost visible day.</summary>
    public bool ContinuesLeft { get; init; }

    /// <summary>True when the item continues past the rightmost visible day.</summary>
    public bool ContinuesRight { get; init; }

    /// <summary>The underlying appointment.</summary>
    public IScheduleItem? Item { get; init; }
}

/// <summary>One visible day laid out in the header band. Carried by <see cref="InfiniteScheduleHeaderContext"/>.</summary>
public readonly struct InfiniteScheduleHeaderDay
{
    /// <summary>The calendar day.</summary>
    public DateTime Day { get; init; }

    /// <summary>True when <see cref="Day"/> is today.</summary>
    public bool IsToday { get; init; }

    /// <summary>X of this day's left edge (already scroll-offset), in canvas logical units.</summary>
    public float Left { get; init; }

    /// <summary>Full width of this day (spans its person sub-columns), in logical units.</summary>
    public float Width { get; init; }
}

/// <summary>
/// Everything a renderer needs to paint the whole header band (the blank time-rail corner plus the
/// day/person row). Carried by <see cref="InfiniteScheduleRenderer.DrawHeader"/>. The canvas is
/// already clipped to <c>[0,0] .. [Width, HeaderHeight]</c>.
/// </summary>
public readonly struct InfiniteScheduleHeaderContext
{
    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; }

    /// <summary>Width of the blank time-rail corner on the left (day cells start here).</summary>
    public float RailWidth { get; init; }

    /// <summary>Height of the header band.</summary>
    public float HeaderHeight { get; init; }

    /// <summary>Full logical width of the header band.</summary>
    public float Width { get; init; }

    /// <summary>Persons split into sub-columns per day; null/empty when a single column per day.</summary>
    public IReadOnlyList<IPerson>? Persons { get; init; }

    /// <summary>Width of one person sub-column (= day width / person count); day width when no persons.</summary>
    public float PersonColumnWidth { get; init; }

    /// <summary>The visible days laid out left-to-right.</summary>
    public IReadOnlyList<InfiniteScheduleHeaderDay> Days { get; init; }
}

/// <summary>
/// Everything a renderer needs to paint one page of the one-day header band: the date sitting above
/// the hour rail, and — right of it — either the day's all-day bars or a summary line. Carried by
/// <see cref="InfiniteScheduleRenderer.DrawSingleDayHeader"/>, once per page in flight; the canvas is
/// already clipped to <c>[0,0] .. [Width, BandHeight]</c>.
///
/// <para>The band is a carousel: a page is as wide as the viewport, so at rest one page fills it and
/// its neighbours sit off-screen, and during a swipe the whole band travels with the day. Paint only
/// the page's own span — <c>DateArea.Left</c> to <c>ContentArea.Right</c>, which may run off either
/// edge — or the page sliding in behind gets wiped out.</para>
/// </summary>
public readonly struct InfiniteSingleDayHeaderContext
{
    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; }

    /// <summary>Full logical width of the band.</summary>
    public float Width { get; init; }

    /// <summary>Total band height: the header row plus whatever the all-day bars need.</summary>
    public float BandHeight { get; init; }

    /// <summary>The day the band is labelling.</summary>
    public DateTime Day { get; init; }

    /// <summary>True when <see cref="Day"/> is today.</summary>
    public bool IsToday { get; init; }

    /// <summary>Left cell holding the date, sitting directly above the hour rail.</summary>
    public SKRect DateArea { get; init; }

    /// <summary>Region right of the rail holding the all-day bars, or the summary line when there are none.</summary>
    public SKRect ContentArea { get; init; }

    /// <summary>Persons split into sub-columns; null/empty when the day is a single column.</summary>
    public IReadOnlyList<IPerson>? Persons { get; init; }

    /// <summary>Width of one person sub-column; the content width when there are no persons.</summary>
    public float PersonColumnWidth { get; init; }

    /// <summary>Row along the band's bottom reserved for the person initials; empty when there are no persons.</summary>
    public SKRect PersonArea { get; init; }

    /// <summary>The day's all-day bars, already laid out inside <see cref="ContentArea"/>; empty when none.</summary>
    public IReadOnlyList<InfiniteAllDayBlock> AllDayBars { get; init; }

    /// <summary>The day's counts, for a renderer that wants to compose its own text.</summary>
    public ScheduleDaySummary Summary { get; init; }

    /// <summary>Summary line to draw when there are no all-day bars (from the view's formatter).</summary>
    public string SummaryText { get; init; }
}

/// <summary>
/// Pluggable Skia painter for <see cref="InfiniteScheduleView"/> — the GPU-canvas counterpart of
/// <c>ScheduleViewRenderer</c>. Subclass and override only the hooks you need; each default
/// reproduces the built-in look. For per-type appointment looks, override <see cref="DrawAppointment"/>
/// and switch on <see cref="InfiniteScheduleBlock.Item"/>, falling back to <c>base.DrawAppointment</c>.
/// </summary>
public class InfiniteScheduleRenderer
{
    private static readonly SKTypeface BoldTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default;

    private static readonly SKTypeface RegularTypeface = SKTypeface.Default;

    /// <summary>Shared default renderer used when none is supplied.</summary>
    public static InfiniteScheduleRenderer Default { get; } = new InfiniteScheduleRenderer();

    /// <summary>
    /// Draws one appointment block. Default reproduces the built-in <c>ScheduleView</c> look: an
    /// 18%-alpha fill, a colored left accent bar, a darkened bold title, and a muted time range.
    /// </summary>
    /// <param name="canvas">Target GPU canvas (drawing in logical units).</param>
    /// <param name="block">Geometry, color, theme, and item for the block.</param>
    public virtual void DrawAppointment(SKCanvas canvas, InfiniteScheduleBlock block)
    {
        var rect = block.Rect;
        var theme = block.Theme;
        float titleSize = (float)theme.BlockTitleFontSize;
        float titleBoxH = titleSize + 4f;
        float rangeSize = (float)theme.BlockRangeFontSize;
        float rangeBoxH = rangeSize + 4f;

        using var soft = new SKPaint { Color = block.Color.WithAlpha(46), IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(rect, 6, 6, soft);

        using var bar = new SKPaint { Color = block.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(new SKRect(rect.Left, rect.Top, rect.Left + 3, rect.Bottom), 1.5f, 1.5f, bar);

        canvas.Save();
        canvas.ClipRect(rect);

        if (!string.IsNullOrEmpty(block.Title))
        {
            var titleColor = new SKColor(
                (byte)(block.Color.Red * 0.5),
                (byte)(block.Color.Green * 0.5),
                (byte)(block.Color.Blue * 0.5));
            using var titlePaint = new SKPaint { Color = titleColor, IsAntialias = true };
            using var titleFont = new SKFont(BoldTypeface, titleSize);
            canvas.DrawText(block.Title, rect.Left + 8, rect.Top + 2 + titleSize, SKTextAlign.Left, titleFont, titlePaint);
        }

        if (rect.Height > titleBoxH + rangeBoxH)
        {
            using var rangePaint = new SKPaint { Color = ToSk(theme.Muted), IsAntialias = true };
            using var rangeFont = new SKFont(RegularTypeface, rangeSize);
            string range = $"{FormatTime(block.Start)} – {FormatTime(block.End)}";
            canvas.DrawText(range, rect.Left + 8, rect.Top + titleBoxH + 2 + rangeSize, SKTextAlign.Left, rangeFont, rangePaint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Draws one bar in the all-day panel. Default is a solid pill in the item color with the title
    /// knocked out in white; a bar that runs past the visible range loses its rounding on that side.
    /// Override to restyle all-day bars (the canvas is already clipped to the panel).
    /// </summary>
    /// <param name="canvas">Target GPU canvas (drawing in logical units).</param>
    /// <param name="block">Bar geometry, color, title, and theme.</param>
    public virtual void DrawAllDayBar(SKCanvas canvas, InfiniteAllDayBlock block)
    {
        var rect = block.Rect;
        float titleSize = (float)block.Theme.BlockRangeFontSize;

        // Push the rounding outside the clip on a side that continues, so the bar reads as unbroken.
        var pill = new SKRect(
            block.ContinuesLeft ? rect.Left - 8 : rect.Left,
            rect.Top,
            block.ContinuesRight ? rect.Right + 8 : rect.Right,
            rect.Bottom);

        using var fill = new SKPaint { Color = block.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(pill, 4, 4, fill);

        if (string.IsNullOrEmpty(block.Title))
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(rect);
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(BoldTypeface, titleSize);
        float baseline = rect.MidY + (titleSize * 0.36f);
        canvas.DrawText(block.Title, rect.Left + 6, baseline, SKTextAlign.Left, font, textPaint);
        canvas.Restore();
    }

    /// <summary>
    /// Draws the whole header band — the blank time-rail corner and the day/person row. Default
    /// reproduces the built-in look: day-of-week over the day number (single column), or a spanning
    /// day label above per-person initials with an accent underline (per-person columns); today is
    /// tinted with <c>Theme.Today</c>. Override to restyle the header (the canvas is already clipped
    /// to the header band).
    /// </summary>
    /// <param name="canvas">Target GPU canvas (drawing in logical units).</param>
    /// <param name="ctx">Header geometry, days, persons, and theme.</param>
    public virtual void DrawHeader(SKCanvas canvas, InfiniteScheduleHeaderContext ctx)
    {
        var theme = ctx.Theme;
        using var bg = new SKPaint { Color = ToSk(theme.HeaderBackground ?? theme.Background), Style = SKPaintStyle.Fill };

        // Corner + day-header background across the whole band.
        canvas.DrawRect(new SKRect(0, 0, ctx.Width, ctx.HeaderHeight), bg);

        float primarySize = (float)theme.HeaderPrimaryFontSize;
        float secondarySize = (float)theme.HeaderSecondaryFontSize;
        using var primaryFont = new SKFont { Size = primarySize };
        using var secondaryFont = new SKFont { Size = secondarySize };
        using var textPaint = new SKPaint { IsAntialias = true };
        using var accent = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var sep = new SKPaint { Color = ToSk(theme.GridLine), StrokeWidth = 0.5f, IsAntialias = false };

        var persons = ctx.Persons is { Count: > 0 } ? ctx.Persons : null;
        int personCount = persons?.Count ?? 1;
        float headerHeight = ctx.HeaderHeight;
        float columnWidth = ctx.PersonColumnWidth;

        // Keep day cells out of the blank corner (the first partially-scrolled day can reach left of the rail).
        canvas.Save();
        canvas.ClipRect(new SKRect(ctx.RailWidth, 0, ctx.Width, headerHeight));

        foreach (var slot in ctx.Days)
        {
            var day = slot.Day;
            bool isToday = slot.IsToday;
            float dayLeft = slot.Left;
            float dayCx = dayLeft + (slot.Width / 2f);
            string dayShort = day.ToString("ddd", CultureInfo.CurrentCulture).ToUpperInvariant();
            string dayNum = day.Day.ToString(CultureInfo.CurrentCulture);

            if (persons is null)
            {
                textPaint.Color = isToday ? ToSk(theme.Today) : ToSk(theme.Foreground);
                canvas.DrawText(dayShort, dayCx, primarySize + 3, SKTextAlign.Center, primaryFont, textPaint);
                canvas.DrawText(dayNum, dayCx, headerHeight - 7, SKTextAlign.Center, secondaryFont, textPaint);
                continue;
            }

            textPaint.Color = isToday ? ToSk(theme.Today) : ToSk(theme.Muted);
            canvas.DrawText($"{dayShort} {dayNum}", dayCx, primarySize + 3, SKTextAlign.Center, primaryFont, textPaint);

            for (int pIdx = 0; pIdx < personCount; pIdx++)
            {
                var person = persons[pIdx];
                float cLeft = dayLeft + (pIdx * columnWidth);
                float cCx = cLeft + (columnWidth / 2f);

                if (pIdx > 0)
                {
                    canvas.DrawLine(cLeft, 0, cLeft, headerHeight, sep);
                }

                if (person.Color is { } pc)
                {
                    accent.Color = ToSk(pc);
                    canvas.DrawRect(cLeft + 2, headerHeight - 3, columnWidth - 4, 2, accent);
                }

                textPaint.Color = isToday ? ToSk(theme.Today) : ToSk(person.Color ?? theme.Foreground);
                canvas.DrawText(ScheduleColumnBuilder.Initials(person.Name), cCx, headerHeight - 7, SKTextAlign.Center, secondaryFont, textPaint);
            }
        }

        canvas.Restore();
    }

    /// <summary>
    /// Draws the one-day header band (<c>ViewMode == 1</c> with <c>SingleDayHeader</c> on). Default:
    /// weekday over day number above the hour rail, and right of it the day's all-day bars — or, when
    /// the day has none, the summary line ("3 appointments" / "Nothing planned"). Override to restyle
    /// it, or to draw your own text; <see cref="InfiniteSingleDayHeaderContext.Summary"/> carries the
    /// raw counts. The canvas is already clipped to the band.
    /// </summary>
    /// <param name="canvas">Target GPU canvas (drawing in logical units).</param>
    /// <param name="ctx">Band geometry, day, persons, all-day bars, and summary text.</param>
    public virtual void DrawSingleDayHeader(SKCanvas canvas, InfiniteSingleDayHeaderContext ctx)
    {
        var theme = ctx.Theme;

        // Paint this page's own span only — the band is a carousel, and the page swiping in behind
        // must not be wiped out by the one in front of it.
        float left = ctx.DateArea.Left;
        float right = ctx.ContentArea.Right;
        using var bg = new SKPaint { Color = ToSk(theme.HeaderBackground ?? theme.Background), Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(left, 0, right, ctx.BandHeight), bg);

        using var sep = new SKPaint { Color = ToSk(theme.GridLine), StrokeWidth = 1, IsAntialias = false };
        canvas.DrawLine(left, ctx.BandHeight - 0.5f, right, ctx.BandHeight - 0.5f, sep);

        DrawSingleDayDate(canvas, ctx);

        if (ctx.AllDayBars is { Count: > 0 } bars)
        {
            canvas.Save();
            canvas.ClipRect(ctx.ContentArea);
            foreach (var bar in bars)
            {
                DrawAllDayBar(canvas, bar);
            }

            canvas.Restore();
        }
        else if (!string.IsNullOrEmpty(ctx.SummaryText))
        {
            float size = (float)theme.HeaderPrimaryFontSize;
            using var summaryPaint = new SKPaint { Color = ToSk(theme.Muted), IsAntialias = true };
            using var summaryFont = new SKFont(RegularTypeface, size);
            canvas.Save();
            canvas.ClipRect(ctx.ContentArea);
            canvas.DrawText(
                ctx.SummaryText,
                ctx.ContentArea.Left + 10,
                ctx.ContentArea.MidY + (size * 0.36f),
                SKTextAlign.Left,
                summaryFont,
                summaryPaint);
            canvas.Restore();
        }

        DrawSingleDayPersons(canvas, ctx);
    }

    /// <summary>Draws the date cell of the one-day header: weekday over day number, tinted when today.</summary>
    /// <param name="canvas">Target GPU canvas.</param>
    /// <param name="ctx">The band context.</param>
    protected virtual void DrawSingleDayDate(SKCanvas canvas, InfiniteSingleDayHeaderContext ctx)
    {
        var theme = ctx.Theme;
        var area = ctx.DateArea;
        float weekdaySize = (float)theme.HeaderPrimaryFontSize;
        float numberSize = (float)theme.HeaderSecondaryFontSize;
        string weekday = ctx.Day.ToString("ddd", CultureInfo.CurrentCulture).ToUpperInvariant();
        string number = ctx.Day.Day.ToString(CultureInfo.CurrentCulture);

        // Stack the two lines about the cell's middle, so the pair stays centered as the band grows.
        float cx = area.MidX;
        float block = weekdaySize + 4 + numberSize;
        float top = area.MidY - (block / 2f);

        using var paint = new SKPaint { Color = ToSk(ctx.IsToday ? theme.Today : theme.Muted), IsAntialias = true };
        using var weekdayFont = new SKFont(RegularTypeface, weekdaySize);
        canvas.DrawText(weekday, cx, top + weekdaySize, SKTextAlign.Center, weekdayFont, paint);

        paint.Color = ToSk(ctx.IsToday ? theme.Today : theme.Foreground);
        using var numberFont = new SKFont(BoldTypeface, numberSize);
        canvas.DrawText(number, cx, top + block, SKTextAlign.Center, numberFont, paint);
    }

    /// <summary>Draws the person initials row along the bottom of the one-day header; no-op without persons.</summary>
    /// <param name="canvas">Target GPU canvas.</param>
    /// <param name="ctx">The band context.</param>
    protected virtual void DrawSingleDayPersons(SKCanvas canvas, InfiniteSingleDayHeaderContext ctx)
    {
        if (ctx.Persons is not { Count: > 0 } persons || ctx.PersonArea.Height <= 0)
        {
            return;
        }

        var theme = ctx.Theme;
        var area = ctx.PersonArea;
        float size = (float)theme.HeaderPrimaryFontSize;

        using var paint = new SKPaint { IsAntialias = true };
        using var accent = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var sep = new SKPaint { Color = ToSk(theme.GridLine), StrokeWidth = 0.5f, IsAntialias = false };
        using var font = new SKFont(RegularTypeface, size);

        canvas.Save();
        canvas.ClipRect(area);

        for (int i = 0; i < persons.Count; i++)
        {
            var person = persons[i];
            float left = area.Left + (i * ctx.PersonColumnWidth);

            if (i > 0)
            {
                canvas.DrawLine(left, area.Top, left, area.Bottom, sep);
            }

            if (person.Color is { } pc)
            {
                accent.Color = ToSk(pc);
                canvas.DrawRect(left + 2, area.Bottom - 3, ctx.PersonColumnWidth - 4, 2, accent);
            }

            paint.Color = ToSk(ctx.IsToday ? theme.Today : (person.Color ?? theme.Foreground));
            canvas.DrawText(
                ScheduleColumnBuilder.Initials(person.Name),
                left + (ctx.PersonColumnWidth / 2f),
                area.Bottom - 6,
                SKTextAlign.Center,
                font,
                paint);
        }

        canvas.Restore();
    }

    /// <summary>Draws the typing draft overlay. Default: translucent fill with a white border.</summary>
    /// <param name="canvas">Target GPU canvas.</param>
    /// <param name="block">Geometry, color, theme for the draft.</param>
    public virtual void DrawTypingItem(SKCanvas canvas, InfiniteScheduleBlock block)
    {
        using var fill = new SKPaint { Color = block.Color.WithAlpha(140), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var border = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawRoundRect(block.Rect, 6, 6, fill);
        canvas.DrawRoundRect(block.Rect, 6, 6, border);
        DrawTitle(canvas, block, SKColors.White);
    }

    /// <summary>Draws the holding ghost overlay. Default: translucent fill with a dashed accent border.</summary>
    /// <param name="canvas">Target GPU canvas.</param>
    /// <param name="block">Geometry, color, theme; <see cref="InfiniteScheduleBlock.IsDragging"/> set while dragged.</param>
    public virtual void DrawHoldingItem(SKCanvas canvas, InfiniteScheduleBlock block)
    {
        using var dash = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0);
        using var fill = new SKPaint { Color = block.Color.WithAlpha(80), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var border = new SKPaint { Color = block.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = dash };
        canvas.DrawRoundRect(block.Rect, 6, 6, fill);
        canvas.DrawRoundRect(block.Rect, 6, 6, border);
        DrawTitle(canvas, block, SKColors.White);
    }

    /// <summary>Draws the block title clipped to its rect (shared helper for the overlay hooks).</summary>
    /// <param name="canvas">Target GPU canvas.</param>
    /// <param name="block">The block whose title to draw.</param>
    /// <param name="color">Text color.</param>
    protected static void DrawTitle(SKCanvas canvas, InfiniteScheduleBlock block, SKColor color)
    {
        if (string.IsNullOrEmpty(block.Title))
        {
            return;
        }

        float size = (float)block.Theme.BlockTitleFontSize;
        using var text = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(BoldTypeface, size);
        canvas.Save();
        canvas.ClipRect(block.Rect);
        canvas.DrawText(block.Title, block.Rect.Left + 6, block.Rect.Top + size + 4, SKTextAlign.Left, font, text);
        canvas.Restore();
    }

    /// <summary>Formats a time as a short lowercase "h tt" / "h:mm tt" string (matches ScheduleView).</summary>
    /// <param name="t">Time to format.</param>
    protected static string FormatTime(DateTime t)
        => (t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture)
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture)).ToLowerInvariant();

    private static SKColor ToSk(Color? color)
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
