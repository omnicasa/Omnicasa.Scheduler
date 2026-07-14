#if ANDROID || IOS
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
