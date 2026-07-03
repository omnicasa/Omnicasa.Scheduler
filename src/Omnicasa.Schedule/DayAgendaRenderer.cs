using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Paints a single appointment block (or drag ghost) for <see cref="DayAgendaView"/>. The view
/// computes geometry, hit rect and resize-handle rect; this carries what's needed to paint inside
/// <see cref="Rect"/>.
/// </summary>
public sealed class DayAgendaAppointmentContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>The appointment being drawn. Switch on its type / <see cref="Appointment.UserData"/> for per-type looks.</summary>
    public Appointment Item { get; init; } = null!;

    /// <summary>Rectangle the block occupies.</summary>
    public RectF Rect { get; init; }

    /// <summary>Resolved background color (item color, else column accent, else theme accent).</summary>
    public Color BlockColor { get; init; } = Colors.Gray;

    /// <summary>Active theme.</summary>
    public ScheduleTheme Theme { get; init; } = new ScheduleTheme();

    /// <summary>Font scale derived from the current hour height (1.0 at the default 60px/hour).</summary>
    public float FontScale { get; init; } = 1f;

    /// <summary>Start time shown in the label (the live drag time when <see cref="IsGhost"/>).</summary>
    public DateTime DisplayStart { get; init; }

    /// <summary>End time shown in the label (the live drag time when <see cref="IsGhost"/>).</summary>
    public DateTime DisplayEnd { get; init; }

    /// <summary>True when this is the translucent drag ghost rather than a placed block.</summary>
    public bool IsGhost { get; init; }

    /// <summary>True when the resize handle pill should be painted (placed blocks tall enough to resize).</summary>
    public bool ShowResizeHandle { get; init; }
}

/// <summary>
/// Pluggable painter for <see cref="DayAgendaView"/>. Subclass and override only the primitives you
/// need; each method's default reproduces the built-in look. For appointments that should render
/// differently per type, override <see cref="DrawAppointment"/> and switch on
/// <see cref="DayAgendaAppointmentContext.Item"/>, falling back to <c>base.DrawAppointment(ctx)</c>.
/// </summary>
public class DayAgendaRenderer
{
    /// <summary>Shared default renderer instance used when none is supplied.</summary>
    public static DayAgendaRenderer Default { get; } = new DayAgendaRenderer();

    /// <summary>Fills the canvas background.</summary>
    public virtual void DrawBackground(ICanvas canvas, RectF dirtyRect, ScheduleTheme theme)
    {
        canvas.FillColor = theme.Background;
        canvas.FillRectangle(0, 0, dirtyRect.Width, dirtyRect.Height);
    }

    /// <summary>Draws the header bar: day-group labels, per-column sub-headers, today tint and accent strips.</summary>
    public virtual void DrawHeader(
        ICanvas canvas,
        float contentX,
        float colW,
        int n,
        float fontScale,
        IReadOnlyList<AgendaColumn> columns,
        ScheduleTheme theme,
        float headerHeight)
    {
        if (headerHeight <= 0 || columns.Count == 0)
        {
            return;
        }

        float primarySize = 12 * fontScale;
        float primaryBoxH = 16 * fontScale;

        // Per-column background tint (today) and accent strip.
        for (int i = 0; i < n; i++)
        {
            var col = columns[i];
            float x0 = contentX + (i * colW);

            if (col.IsToday)
            {
                var tint = col.Accent ?? theme.Today;
                canvas.FillColor = new Color(tint.Red, tint.Green, tint.Blue, 0.1f);
                canvas.FillRectangle(x0, 0, colW, headerHeight);
            }

            if (col.Accent is { } a)
            {
                canvas.FillColor = a;
                canvas.FillRectangle(x0 + 2, headerHeight - 3, colW - 4, 2);
            }
        }

        // Top row: group consecutive columns sharing DayStart and draw the primary label once per group.
        int gi = 0;
        while (gi < n)
        {
            int gj = gi;
            while (gj + 1 < n && columns[gj + 1].DayStart == columns[gi].DayStart)
            {
                gj++;
            }

            var groupCol = columns[gi];
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

            // Vertical tick between day groups so adjacent days are visually separated.
            if (gj + 1 < n)
            {
                canvas.StrokeColor = theme.GridLine;
                canvas.StrokeSize = 0.5f;
                canvas.DrawLine(gx + gw, 0, gx + gw, headerHeight);
            }

            gi = gj + 1;
        }

        // Bottom row: per-column secondary label (day number, or person name in persons mode).
        for (int i = 0; i < n; i++)
        {
            var col = columns[i];
            float x0 = contentX + (i * colW);
            if (string.IsNullOrEmpty(col.HeaderSecondary))
            {
                continue;
            }

            bool secondaryIsShort = col.HeaderSecondary.Length <= 4;
            float secondarySize = (secondaryIsShort ? 18 : 13) * fontScale;
            float secondaryBoxH = (secondaryIsShort ? 26 : 18) * fontScale;
            Color secondaryColor;
            if (col.IsToday)
            {
                secondaryColor = col.Accent ?? theme.Today;
            }
            else if (col.Accent is { } a)
            {
                secondaryColor = a;
            }
            else
            {
                secondaryColor = theme.Foreground;
            }

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
        canvas.DrawLine(0, headerHeight, contentX + (colW * n), headerHeight);
    }

    /// <summary>Draws the left time-rail hour labels and the full-width hour grid lines.</summary>
    public virtual void DrawHourGrid(
        ICanvas canvas,
        float width,
        float contentX,
        float fontScale,
        TimeScale scale,
        ScheduleTheme theme,
        float timeRailWidth)
    {
        float hourLabelSize = 11 * fontScale;
        float hourLabelBoxH = 16 * fontScale;

        canvas.StrokeColor = theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.FontColor = theme.Muted;
        canvas.FontSize = hourLabelSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;

        for (int hr = 0; hr <= 24; hr++)
        {
            float y = scale.YForTime(TimeSpan.FromHours(hr));
            canvas.DrawLine(contentX, y, width, y);
            if (hr < 24)
            {
                string label = HourLabelFormatter.Format(hr, theme.HourLabelFormat);

                canvas.DrawString(
                    label,
                    4,
                    y - (hourLabelBoxH / 2f),
                    timeRailWidth - 8,
                    hourLabelBoxH,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Top);
            }
        }
    }

    /// <summary>Draws the vertical separators between columns (from the header down).</summary>
    public virtual void DrawColumnSeparators(
        ICanvas canvas,
        float contentX,
        float colW,
        int n,
        float h,
        float headerHeight,
        ScheduleTheme theme)
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
            canvas.DrawLine(x, headerHeight, x, h);
        }
    }

    /// <summary>Draws the "now" marker (dot + line) across every column whose day is today.</summary>
    public virtual void DrawTodayMarker(
        ICanvas canvas,
        float contentX,
        float colW,
        int n,
        IReadOnlyList<AgendaColumn> columns,
        DateTime now,
        TimeScale scale,
        ScheduleTheme theme)
    {
        var today = DateOnly.FromDateTime(now);
        for (int i = 0; i < n; i++)
        {
            var col = columns[i];
            if (DateOnly.FromDateTime(col.DayStart) != today)
            {
                continue;
            }

            float y = scale.YForTime(now.TimeOfDay);
            float x0 = contentX + (i * colW);
            float x1 = x0 + colW;
            var markerColor = col.Accent ?? theme.Today;
            canvas.FillColor = markerColor;
            canvas.FillCircle(x0, y, 4);
            canvas.StrokeColor = markerColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(x0, y, x1, y);
        }
    }

    /// <summary>
    /// Draws a single appointment block (or drag ghost) inside <see cref="DayAgendaAppointmentContext.Rect"/>.
    /// Override and switch on <see cref="DayAgendaAppointmentContext.Item"/> for per-type looks.
    /// </summary>
    public virtual void DrawAppointment(DayAgendaAppointmentContext ctx)
    {
        var canvas = ctx.Canvas;
        var theme = ctx.Theme;
        var rect = ctx.Rect;
        float x = rect.Left;
        float y1 = rect.Top;
        float rw = rect.Width;
        float rh = rect.Height;

        float fontScale = ctx.FontScale;
        float titleSize = 12 * fontScale;
        float titleBoxH = 16 * fontScale;
        float rangeSize = 10 * fontScale;
        float rangeBoxH = 14 * fontScale;

        var bg = ctx.BlockColor;
        var bgSoft = new Color(bg.Red, bg.Green, bg.Blue, ctx.IsGhost ? 0.35f : 0.18f);
        canvas.FillColor = bgSoft;
        canvas.FillRoundedRectangle(rect, 6);

        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(new RectF(x, y1, 3, rh), 1.5f);

        var textColor = new Color(bg.Red * 0.5f, bg.Green * 0.5f, bg.Blue * 0.5f);
        canvas.FontColor = textColor;
        canvas.FontSize = titleSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            ctx.Item.Title ?? string.Empty,
            new RectF(x + 8, y1 + 2, rw - 10, MathF.Min(titleBoxH, rh - 4)),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > titleBoxH + rangeBoxH)
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = rangeSize;
            canvas.FontColor = theme.Muted;
            var range = $"{FormatTime(ctx.DisplayStart)} – {FormatTime(ctx.DisplayEnd)}";
            canvas.DrawString(
                range,
                new RectF(x + 8, y1 + titleBoxH + 2, rw - 10, rangeBoxH),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }

        if (ctx.ShowResizeHandle)
        {
            var handle = new RectF(x + rw - 16, (y1 + rh) - 8, 12, 4);
            canvas.FillColor = textColor;
            canvas.Alpha = 0.5f;
            canvas.FillRoundedRectangle(handle, 2);
            canvas.Alpha = 1f;
        }
    }

    /// <summary>Formats a time as a short "h tt" / "h:mm tt" lowercase string.</summary>
    protected static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }
}
