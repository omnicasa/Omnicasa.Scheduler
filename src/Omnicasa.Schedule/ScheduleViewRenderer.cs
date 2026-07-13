using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Paints a single appointment block for <see cref="ScheduleView"/>. The view computes the
/// geometry (and hit rect); this carries everything needed to paint inside <see cref="Rect"/>.
/// </summary>
public sealed class ScheduleAppointmentContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>The item being drawn. Switch on its concrete type for per-type rendering.</summary>
    public IScheduleItem Item { get; init; } = null!;

    /// <summary>Rectangle the block occupies (already clipped to the day and overlap-column).</summary>
    public RectF Rect { get; init; }

    /// <summary>Resolved background color (item color, else column accent, else theme accent).</summary>
    public Color BlockColor { get; init; } = Colors.Gray;

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; } = new ScheduleViewTheme();
}

/// <summary>
/// Paints the in-progress "typing" / draft block (with drag handles) for <see cref="ScheduleView"/>.
/// </summary>
public sealed class ScheduleTypingContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>The draft item being drawn.</summary>
    public ITypingScheduleItem Item { get; init; } = null!;

    /// <summary>Rectangle the draft occupies.</summary>
    public RectF Rect { get; init; }

    /// <summary>Resolved background color (item color, else column accent, else theme accent).</summary>
    public Color BlockColor { get; init; } = Colors.Gray;

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; } = new ScheduleViewTheme();
}

/// <summary>
/// Paints the "holding" block for <see cref="ScheduleView"/> — an item that floats and is dragged
/// (free vertically, snapped to a column). Drawn whenever <c>ScheduleView.HoldingSchedule</c> is set.
/// </summary>
public sealed class ScheduleHoldingContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>The held item being drawn.</summary>
    public IScheduleItem Item { get; init; } = null!;

    /// <summary>Rectangle the block occupies (natural position, or the current drag position).</summary>
    public RectF Rect { get; init; }

    /// <summary>Start time currently shown (the dragged time while dragging, else the item's start).</summary>
    public DateTime DisplayStart { get; init; }

    /// <summary>End time currently shown.</summary>
    public DateTime DisplayEnd { get; init; }

    /// <summary>Resolved background color (item color, else column accent, else theme accent).</summary>
    public Color BlockColor { get; init; } = Colors.Gray;

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; } = new ScheduleViewTheme();

    /// <summary>True while the user is actively dragging the block.</summary>
    public bool IsDragging { get; init; }
}

/// <summary>Paints one all-day / cross-date bar in the panel above <see cref="ScheduleView"/>'s grid.</summary>
public sealed class ScheduleAllDayContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>The all-day / multi-day item being drawn.</summary>
    public IScheduleItem Item { get; init; } = null!;

    /// <summary>Rectangle the bar occupies (spanning its day columns, within its lane).</summary>
    public RectF Rect { get; init; }

    /// <summary>Resolved bar color (item color, else theme accent).</summary>
    public Color BlockColor { get; init; } = Colors.Gray;

    /// <summary>Active theme (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; init; } = new ScheduleViewTheme();
}

/// <summary>
/// Pluggable painter for <see cref="ScheduleView"/>. Subclass and override only the primitives you
/// need; each method's default reproduces the built-in look. For appointments that should render
/// differently per type, override <see cref="DrawAppointment"/> and switch on
/// <see cref="ScheduleAppointmentContext.Item"/>, falling back to <c>base.DrawAppointment(ctx)</c>.
/// </summary>
public class ScheduleViewRenderer
{
    /// <summary>Shared default renderer instance used when none is supplied.</summary>
    public static ScheduleViewRenderer Default { get; } = new ScheduleViewRenderer();

    /// <summary>Fills the canvas background. Called by the body and all-day canvases.</summary>
    public virtual void DrawBackground(ICanvas canvas, RectF dirtyRect, ScheduleViewTheme theme)
    {
        canvas.FillColor = theme.Background;
        canvas.FillRectangle(0, 0, dirtyRect.Width, dirtyRect.Height);
    }

    /// <summary>
    /// Fills the sticky header bar's background, before <see cref="DrawHeader"/> paints its content.
    /// Default uses <see cref="ScheduleViewTheme.HeaderBackground"/> when set, else delegates to
    /// <see cref="DrawBackground"/> so the header matches the body. Override for a surface a flat
    /// color can't give (gradient, image) without re-implementing <see cref="DrawHeader"/>. Not
    /// called when the header canvas is in transparent mode
    /// (<c>ScheduleHeaderDrawable.DrawsBackground = false</c>).
    /// </summary>
    public virtual void DrawHeaderBackground(ICanvas canvas, RectF dirtyRect, ScheduleRenderContext ctx)
    {
        if (ctx.Theme.HeaderBackground is { } header)
        {
            canvas.FillColor = header;
            canvas.FillRectangle(0, 0, dirtyRect.Width, dirtyRect.Height);
            return;
        }

        DrawBackground(canvas, dirtyRect, ctx.Theme);
    }

    /// <summary>Draws the sticky header bar: day-group labels, per-column sub-headers, today tint and accent strips.</summary>
    public virtual void DrawHeader(ICanvas canvas, RectF dirtyRect, ScheduleRenderContext ctx)
    {
        var theme = ctx.Theme;
        float w = dirtyRect.Width;
        int n = ctx.Columns.Count;

        if (n == 0 || ctx.HeaderHeight <= 0)
        {
            return;
        }

        float contentX = ctx.TimeRailWidth;
        float contentW = w - contentX;
        if (contentW <= 0)
        {
            return;
        }

        float colW = contentW / n;
        float headerH = ctx.HeaderHeight;
        float primarySize = (float)theme.HeaderPrimaryFontSize;
        float primaryBoxH = primarySize + 4f;

        for (int i = 0; i < n; i++)
        {
            var col = ctx.Columns[i];
            float x0 = contentX + (i * colW);

            if (col.IsToday)
            {
                var tint = col.Accent ?? theme.Today;
                canvas.FillColor = new Color(tint.Red, tint.Green, tint.Blue, 0.1f);
                canvas.FillRectangle(x0, 0, colW, headerH);
            }

            if (col.Accent is { } a)
            {
                canvas.FillColor = a;
                canvas.FillRectangle(x0 + 2, headerH - 3, colW - 4, 2);
            }
        }

        int gi = 0;
        while (gi < n)
        {
            int gj = gi;
            while (gj + 1 < n && ctx.Columns[gj + 1].DayStart == ctx.Columns[gi].DayStart)
            {
                gj++;
            }

            var groupCol = ctx.Columns[gi];
            float gx = contentX + (gi * colW);
            float gw = (gj - gi + 1) * colW;
            canvas.FontColor = groupCol.IsToday ? (groupCol.Accent ?? theme.Today) : theme.Muted;
            canvas.FontSize = primarySize;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.DrawString(
                groupCol.HeaderPrimary,
                gx + 2,
                4,
                gw - 4,
                primaryBoxH,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);

            if (gj + 1 < n)
            {
                canvas.StrokeColor = theme.GridLine;
                canvas.StrokeSize = 0.5f;
                canvas.DrawLine(gx + gw, 0, gx + gw, headerH);
            }

            gi = gj + 1;
        }

        for (int i = 0; i < n; i++)
        {
            var col = ctx.Columns[i];
            float x0 = contentX + (i * colW);
            if (string.IsNullOrEmpty(col.HeaderSecondary))
            {
                continue;
            }

            bool secondaryIsShort = col.HeaderSecondary.Length <= 4;
            float secondarySize = secondaryIsShort
                ? (float)theme.HeaderSecondaryFontSize
                : (float)(theme.HeaderSecondaryFontSize * 0.72);
            float secondaryBoxH = secondarySize + 8f;

            Color secondaryColor = col.IsToday
                ? (col.Accent ?? theme.Today)
                : (col.Accent ?? theme.Foreground);

            canvas.FontColor = secondaryColor;
            canvas.FontSize = secondarySize;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            canvas.DrawString(
                col.HeaderSecondary,
                x0 + 2,
                4 + primaryBoxH,
                colW - 4,
                secondaryBoxH,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }

        canvas.StrokeColor = theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.DrawLine(0, headerH, contentX + (colW * n), headerH);
    }

    /// <summary>Draws the left time-rail hour labels and the full-width hour grid lines.</summary>
    public virtual void DrawHourGrid(ICanvas canvas, float width, float contentX, ScheduleRenderContext ctx)
    {
        var theme = ctx.Theme;
        var scale = ctx.Scale;
        float labelSize = (float)theme.HourLabelFontSize;
        float labelBoxH = labelSize + 4f;

        canvas.StrokeColor = theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.FontColor = theme.Muted;
        canvas.FontSize = labelSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;

        for (int hr = 0; hr <= 24; hr++)
        {
            float y = scale.YForTime(TimeSpan.FromHours(hr));
            canvas.DrawLine(contentX, y, width, y);

            string label = HourLabelFormatter.Format(hr, theme.HourLabelFormat);

            // Keep the 00:00 / 24:00 labels inside the canvas when there is no inset to center in.
            float labelY = Math.Clamp(y - (labelBoxH / 2f), 0f, scale.TotalHeight - labelBoxH);

            canvas.DrawString(
                label,
                4,
                labelY,
                ctx.TimeRailWidth - 8,
                labelBoxH,
                HorizontalAlignment.Right,
                VerticalAlignment.Top);
        }
    }

    /// <summary>
    /// Draws the header strip inside the scrollable body — the <c>TopContentInset</c> area above
    /// the 00:00 line (rect spans the full canvas width). Default draws nothing (the body
    /// background shows through). Only called when the inset is &gt; 0.
    /// </summary>
    public virtual void DrawBodyHeader(ICanvas canvas, RectF rect, ScheduleRenderContext ctx)
    {
    }

    /// <summary>
    /// Draws the footer strip inside the scrollable body — the <c>BottomContentInset</c> area
    /// below the 24:00 line (rect spans the full canvas width). Default draws nothing (the body
    /// background shows through). Only called when the inset is &gt; 0.
    /// </summary>
    public virtual void DrawBodyFooter(ICanvas canvas, RectF rect, ScheduleRenderContext ctx)
    {
    }

    /// <summary>Draws the vertical separators between columns.</summary>
    public virtual void DrawColumnSeparators(ICanvas canvas, float contentX, float colW, int n, float h, ScheduleViewTheme theme)
    {
        if (n <= 1)
        {
            return;
        }

        canvas.StrokeColor = theme.GridLine;
        canvas.StrokeSize = 0.5f;
        for (int i = 1; i < n; i++)
        {
            float x = contentX + (i * colW);
            canvas.DrawLine(x, 0, x, h);
        }
    }

    /// <summary>
    /// Draws the "now" marker when today is visible: a capsule on the time rail showing the
    /// current time (honouring <see cref="ScheduleViewTheme.HourLabelFormat"/>) plus a line
    /// across the full schedule width, in <see cref="ScheduleViewTheme.NowIndicator"/>.
    /// </summary>
    public virtual void DrawTodayMarker(ICanvas canvas, float contentX, float colW, ScheduleRenderContext ctx)
    {
        if (!ctx.ShowNowIndicator || ctx.Now is not { } now)
        {
            return;
        }

        int n = ctx.Columns.Count;
        var today = DateOnly.FromDateTime(now);
        bool todayVisible = false;
        for (int i = 0; i < n; i++)
        {
            if (DateOnly.FromDateTime(ctx.Columns[i].DayStart) == today)
            {
                todayVisible = true;
                break;
            }
        }

        if (!todayVisible)
        {
            return;
        }

        var theme = ctx.Theme;
        var markerColor = theme.NowIndicator;
        float y = ctx.Scale.YForTime(now.TimeOfDay);

        canvas.StrokeColor = markerColor;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(contentX, y, contentX + (n * colW), y);

        string label = string.IsNullOrEmpty(theme.HourLabelFormat)
            ? FormatTime(now)
            : HourLabelFormatter.Custom(now, theme.HourLabelFormat);
        float fontSize = (float)theme.HourLabelFontSize;
        float badgeH = fontSize + 8f;
        float badgeW = ctx.TimeRailWidth - 6f;
        float badgeX = 2f;
        float badgeY = y - (badgeH / 2f);

        canvas.FillColor = markerColor;
        canvas.FillRoundedRectangle(badgeX, badgeY, badgeW, badgeH, badgeH / 2f);
        canvas.FontColor = Colors.White;
        canvas.FontSize = fontSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.DrawString(label, badgeX, badgeY, badgeW, badgeH, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    /// <summary>
    /// Draws a single appointment block inside <see cref="ScheduleAppointmentContext.Rect"/>.
    /// Override and switch on <see cref="ScheduleAppointmentContext.Item"/> for per-type looks.
    /// </summary>
    public virtual void DrawAppointment(ScheduleAppointmentContext ctx)
    {
        var canvas = ctx.Canvas;
        var theme = ctx.Theme;
        var item = ctx.Item;
        var rect = ctx.Rect;
        float x = rect.Left;
        float y1 = rect.Top;
        float rw = rect.Width;
        float rh = rect.Height;

        float titleSize = (float)theme.BlockTitleFontSize;
        float titleBoxH = titleSize + 4f;
        float rangeSize = (float)theme.BlockRangeFontSize;
        float rangeBoxH = rangeSize + 4f;

        var bg = ctx.BlockColor;
        var bgSoft = new Color(bg.Red, bg.Green, bg.Blue, 0.18f);
        canvas.FillColor = bgSoft;
        canvas.FillRoundedRectangle(rect, 6);

        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(new RectF(x, y1, 3, rh), 1.5f);

        var textColor = new Color(bg.Red * 0.5f, bg.Green * 0.5f, bg.Blue * 0.5f);
        canvas.FontColor = textColor;
        canvas.FontSize = titleSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            item.Title ?? string.Empty,
            new RectF(x + 8, y1 + 2, rw - 10, MathF.Min(titleBoxH, rh - 4)),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > titleBoxH + rangeBoxH)
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = rangeSize;
            canvas.FontColor = theme.Muted;
            var range = $"{FormatTime(item.Start)} – {FormatTime(item.End)}";
            canvas.DrawString(
                range,
                new RectF(x + 8, y1 + titleBoxH + 2, rw - 10, rangeBoxH),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }
    }

    /// <summary>Draws the draft / typing block (shadowed box, corner drag handles, labels).</summary>
    public virtual void DrawTypingItem(ScheduleTypingContext ctx)
    {
        var canvas = ctx.Canvas;
        var theme = ctx.Theme;
        var typing = ctx.Item;
        var rect = ctx.Rect;
        float x = rect.Left;
        float y1 = rect.Top;
        float rw = rect.Width;
        float rh = rect.Height;
        var bg = ctx.BlockColor;

        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, 3), 8, new Color(0, 0, 0, 0.35f));
        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(rect, 8);
        canvas.RestoreState();

        // Corner handles: small circles with red border, white fill — top-left + bottom-right.
        const float handleRadius = 7f;
        var borderColor = theme.Today;
        float topLeftCx = x + handleRadius;
        float topLeftCy = y1 + handleRadius;
        float bottomRightCx = (x + rw) - handleRadius;
        float bottomRightCy = (y1 + rh) - handleRadius;

        canvas.FillColor = Colors.White;
        canvas.FillCircle(topLeftCx, topLeftCy, handleRadius);
        canvas.FillCircle(bottomRightCx, bottomRightCy, handleRadius);

        canvas.StrokeColor = borderColor;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(topLeftCx, topLeftCy, handleRadius);
        canvas.DrawCircle(bottomRightCx, bottomRightCy, handleRadius);

        // Title + range text in white-ish for contrast on the saturated background.
        var textColor = new Color(1f, 1f, 1f, 0.95f);
        float titleSize = (float)theme.BlockTitleFontSize;
        float titleBoxH = titleSize + 4f;
        float rangeSize = (float)theme.BlockRangeFontSize;
        float rangeBoxH = rangeSize + 4f;

        float textTopY = y1 + (2f * handleRadius) + 4f;
        canvas.FontColor = textColor;
        canvas.FontSize = titleSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            typing.Title ?? string.Empty,
            new RectF(x + 10, textTopY, rw - 20, MathF.Min(titleBoxH, (y1 + rh) - textTopY - (2f * handleRadius))),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > (4f * handleRadius) + titleBoxH + rangeBoxH + 4f)
        {
            canvas.FontSize = rangeSize;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            var range = $"{FormatTime(typing.Start)} – {FormatTime(typing.End)}";
            canvas.DrawString(
                range,
                new RectF(x + 10, textTopY + titleBoxH, rw - 20, rangeBoxH),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }
    }

    /// <summary>
    /// Draws the holding block (a floating, draggable item). Default is a shadowed rounded block with
    /// title + time range; it lifts slightly while dragging. Override and switch on
    /// <see cref="ScheduleHoldingContext.Item"/> for a custom look.
    /// </summary>
    public virtual void DrawHoldingItem(ScheduleHoldingContext ctx)
    {
        var canvas = ctx.Canvas;
        var theme = ctx.Theme;
        var rect = ctx.Rect;
        float x = rect.Left;
        float y1 = rect.Top;
        float rw = rect.Width;
        float rh = rect.Height;
        var bg = ctx.BlockColor;

        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, ctx.IsDragging ? 6 : 3), ctx.IsDragging ? 12 : 8, new Color(0, 0, 0, 0.35f));
        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(rect, 8);
        canvas.RestoreState();

        // Resize handles: top-left (start) and bottom-right (end), matching the typing block.
        const float handleRadius = 7f;
        var borderColor = theme.Today;
        float topLeftCx = x + handleRadius;
        float topLeftCy = y1 + handleRadius;
        float bottomRightCx = (x + rw) - handleRadius;
        float bottomRightCy = (y1 + rh) - handleRadius;

        canvas.FillColor = Colors.White;
        canvas.FillCircle(topLeftCx, topLeftCy, handleRadius);
        canvas.FillCircle(bottomRightCx, bottomRightCy, handleRadius);
        canvas.StrokeColor = borderColor;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(topLeftCx, topLeftCy, handleRadius);
        canvas.DrawCircle(bottomRightCx, bottomRightCy, handleRadius);

        var textColor = new Color(1f, 1f, 1f, 0.95f);
        float titleSize = (float)theme.BlockTitleFontSize;
        float titleBoxH = titleSize + 4f;
        float rangeSize = (float)theme.BlockRangeFontSize;
        float rangeBoxH = rangeSize + 4f;

        float textTopY = y1 + (2f * handleRadius) + 4f;
        canvas.FontColor = textColor;
        canvas.FontSize = titleSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            ctx.Item.Title ?? string.Empty,
            new RectF(x + 10, textTopY, rw - 20, MathF.Min(titleBoxH, (y1 + rh) - textTopY - (2f * handleRadius))),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > (4f * handleRadius) + titleBoxH + rangeBoxH + 4f)
        {
            canvas.FontSize = rangeSize;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            var range = $"{FormatTime(ctx.DisplayStart)} – {FormatTime(ctx.DisplayEnd)}";
            canvas.DrawString(
                range,
                new RectF(x + 10, textTopY + titleBoxH, rw - 20, rangeBoxH),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }
    }

    /// <summary>
    /// Draws one all-day / cross-date bar. Default is a filled rounded bar with the title. Override
    /// and switch on <see cref="ScheduleAllDayContext.Item"/> for a custom look.
    /// </summary>
    public virtual void DrawAllDayItem(ScheduleAllDayContext ctx)
    {
        var canvas = ctx.Canvas;
        var rect = ctx.Rect;
        var bg = ctx.BlockColor;

        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(rect, 4);

        var textColor = new Color(1f, 1f, 1f, 0.95f);
        canvas.FontColor = textColor;
        canvas.FontSize = (float)ctx.Theme.BlockRangeFontSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            ctx.Item.Title ?? string.Empty,
            new RectF(rect.X + 6, rect.Y, rect.Width - 10, rect.Height),
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    /// <summary>Formats a time as a short "h tt" / "h:mm tt" lowercase string.</summary>
    protected static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }
}
