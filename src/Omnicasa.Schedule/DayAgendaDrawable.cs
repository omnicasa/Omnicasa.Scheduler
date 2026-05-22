using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>A single rendered column in the day agenda (a day, a person, or a day+person combo).</summary>
public sealed class AgendaColumn
{
    /// <summary>Gets or sets the start of the day this column belongs to.</summary>
    public DateTime DayStart { get; set; }

    /// <summary>Gets or sets the primary header text (e.g. "MON" or "Alice").</summary>
    public string HeaderPrimary { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional secondary header text (e.g. "24" for day number).</summary>
    public string? HeaderSecondary { get; set; }

    /// <summary>Gets or sets the accent color used in the header (e.g. person color).</summary>
    public Color? Accent { get; set; }

    /// <summary>Gets or sets a value indicating whether this column represents the current date.</summary>
    public bool IsToday { get; set; }

    /// <summary>Gets or sets the laid-out appointments rendered in this column.</summary>
    public IReadOnlyList<LaidOutAppointment> Events { get; set; } = Array.Empty<LaidOutAppointment>();
}

/// <summary>
/// Draws a day agenda with a shared time rail and one or more columns (days and/or persons).
/// </summary>
public sealed class DayAgendaDrawable : IDrawable
{
    private readonly List<(Appointment Item, RectF Rect)> hitMap = new List<(Appointment, RectF)>();

    private readonly List<(Appointment Item, RectF Handle)> resizeHandles = new List<(Appointment, RectF)>();

    /// <summary>Gets or sets the columns rendered left-to-right (after the time rail).</summary>
    public IReadOnlyList<AgendaColumn> Columns { get; set; } = Array.Empty<AgendaColumn>();

    /// <summary>Gets or sets the color theme.</summary>
    public ScheduleTheme Theme { get; set; } = new ScheduleTheme();

    /// <summary>Gets or sets the vertical scale mapping times to Y coordinates.</summary>
    public TimeScale Scale { get; set; } = new TimeScale(60);

    /// <summary>Gets or sets the width of the time rail on the left, in logical pixels.</summary>
    public float TimeRailWidth { get; set; } = 56;

    /// <summary>Gets or sets the header height. Zero hides the header.</summary>
    public float HeaderHeight { get; set; }

    /// <summary>Gets or sets the current time to highlight.</summary>
    public DateTime? Now { get; set; }

    /// <summary>Gets or sets the appointment being dragged, drawn as a translucent ghost.</summary>
    public Appointment? Ghost { get; set; }

    /// <summary>Gets or sets the ghost start time used during drag operations.</summary>
    public DateTime? GhostStart { get; set; }

    /// <summary>Gets or sets the ghost end time used during drag operations.</summary>
    public DateTime? GhostEnd { get; set; }

    /// <summary>Gets or sets the target column index for the ghost during drag (override).</summary>
    public int? GhostColumnIndex { get; set; }

    /// <summary>Gets the hit-test map populated after <see cref="Draw"/>.</summary>
    public IReadOnlyList<(Appointment Item, RectF Rect)> HitMap => hitMap;

    /// <summary>Gets the resize-handle rectangles populated after <see cref="Draw"/>.</summary>
    public IReadOnlyList<(Appointment Item, RectF Handle)> ResizeHandles => resizeHandles;

    /// <summary>Fired at the end of <see cref="Draw"/> so callers can react to the now-current <see cref="HitMap"/>.</summary>
    public event Action? Drawn;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        hitMap.Clear();
        resizeHandles.Clear();

        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        int n = Math.Max(1, Columns.Count);

        canvas.FillColor = Theme.Background;
        canvas.FillRectangle(0, 0, w, h);

        float contentX = TimeRailWidth;
        float contentW = w - TimeRailWidth;
        float colW = contentW / n;
        float fontScale = Scale.HourHeight / 60f;

        DrawColumnHeaders(canvas, contentX, colW, n, fontScale);
        DrawHourGridAndLabels(canvas, w, contentX, fontScale);

        for (int i = 0; i < n; i++)
        {
            float x0 = contentX + (i * colW);
            DrawColumnEvents(canvas, i, x0, colW);
        }

        DrawColumnSeparators(canvas, contentX, colW, n, h);
        DrawTodayMarker(canvas, contentX, colW, n);
        DrawGhost(canvas, contentX, colW, n);

        Drawn?.Invoke();
    }

    private static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }

    private void DrawColumnHeaders(ICanvas canvas, float contentX, float colW, int n, float fontScale)
    {
        if (HeaderHeight <= 0 || Columns.Count == 0)
        {
            return;
        }

        float primarySize = 12 * fontScale;
        float primaryBoxH = 16 * fontScale;

        // Per-column background tint (today) and accent strip.
        for (int i = 0; i < n; i++)
        {
            var col = Columns[i];
            float x0 = contentX + (i * colW);

            if (col.IsToday)
            {
                var tint = col.Accent ?? Theme.Today;
                canvas.FillColor = new Color(tint.Red, tint.Green, tint.Blue, 0.1f);
                canvas.FillRectangle(x0, 0, colW, HeaderHeight);
            }

            if (col.Accent is { } a)
            {
                canvas.FillColor = a;
                canvas.FillRectangle(x0 + 2, HeaderHeight - 3, colW - 4, 2);
            }
        }

        // Top row: group consecutive columns sharing DayStart and draw the primary label once per group.
        int gi = 0;
        while (gi < n)
        {
            int gj = gi;
            while (gj + 1 < n && Columns[gj + 1].DayStart == Columns[gi].DayStart)
            {
                gj++;
            }

            var groupCol = Columns[gi];
            float gx = contentX + (gi * colW);
            float gw = (gj - gi + 1) * colW;
            canvas.FontColor = groupCol.IsToday ? (groupCol.Accent ?? Theme.Today) : Theme.Muted;
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
                canvas.StrokeColor = Theme.GridLine;
                canvas.StrokeSize = 0.5f;
                canvas.DrawLine(gx + gw, 0, gx + gw, HeaderHeight);
            }

            gi = gj + 1;
        }

        // Bottom row: per-column secondary label (day number, or person name in persons mode).
        for (int i = 0; i < n; i++)
        {
            var col = Columns[i];
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
                secondaryColor = col.Accent ?? Theme.Today;
            }
            else if (col.Accent is { } a)
            {
                secondaryColor = a;
            }
            else
            {
                secondaryColor = Theme.Foreground;
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

        canvas.StrokeColor = Theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.DrawLine(0, HeaderHeight, contentX + (colW * n), HeaderHeight);
    }

    private void DrawHourGridAndLabels(ICanvas canvas, float w, float contentX, float fontScale)
    {
        float hourLabelSize = 11 * fontScale;
        float hourLabelBoxH = 16 * fontScale;

        canvas.StrokeColor = Theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.FontColor = Theme.Muted;
        canvas.FontSize = hourLabelSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;

        for (int hr = 0; hr <= 24; hr++)
        {
            float y = Scale.YForTime(TimeSpan.FromHours(hr));
            canvas.DrawLine(contentX, y, w, y);
            if (hr < 24)
            {
                string label;
                if (hr == 0)
                {
                    label = "12 AM";
                }
                else if (hr < 12)
                {
                    label = $"{hr} AM";
                }
                else if (hr == 12)
                {
                    label = "12 PM";
                }
                else
                {
                    label = $"{hr - 12} PM";
                }

                canvas.DrawString(
                    label,
                    4,
                    y - (hourLabelBoxH / 2f),
                    TimeRailWidth - 8,
                    hourLabelBoxH,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Top);
            }
        }
    }

    private void DrawColumnEvents(ICanvas canvas, int columnIndex, float x0, float colW)
    {
        if (columnIndex >= Columns.Count)
        {
            return;
        }

        var column = Columns[columnIndex];
        var dayStart = column.DayStart;
        var dayEnd = dayStart.AddDays(1);

        foreach (var laid in column.Events)
        {
            var a = laid.Appointment;
            DrawBlock(canvas, a, column.Accent, a.Start, a.End, laid.Column, laid.ColumnsInGroup, x0, colW, dayStart, dayEnd, isGhost: false);
        }
    }

    private void DrawColumnSeparators(ICanvas canvas, float contentX, float colW, int n, float h)
    {
        if (n <= 1)
        {
            return;
        }

        canvas.StrokeColor = Theme.GridLine;
        canvas.StrokeSize = 0.5f;
        for (int i = 1; i < n; i++)
        {
            float x = contentX + (i * colW);
            canvas.DrawLine(x, HeaderHeight, x, h);
        }
    }

    private void DrawTodayMarker(ICanvas canvas, float contentX, float colW, int n)
    {
        if (Now is not { } now)
        {
            return;
        }

        var today = DateOnly.FromDateTime(now);
        for (int i = 0; i < n; i++)
        {
            var col = Columns[i];
            if (DateOnly.FromDateTime(col.DayStart) != today)
            {
                continue;
            }

            float y = Scale.YForTime(now.TimeOfDay);
            float x0 = contentX + (i * colW);
            float x1 = x0 + colW;
            var markerColor = col.Accent ?? Theme.Today;
            canvas.FillColor = markerColor;
            canvas.FillCircle(x0, y, 4);
            canvas.StrokeColor = markerColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(x0, y, x1, y);
        }
    }

    private void DrawGhost(ICanvas canvas, float contentX, float colW, int n)
    {
        if (Ghost is null || GhostStart is null || GhostEnd is null)
        {
            return;
        }

        int columnIndex = -1;
        int col = 0;
        int cols = 1;

        if (GhostColumnIndex is int forced && forced >= 0 && forced < n)
        {
            columnIndex = forced;
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                foreach (var l in Columns[i].Events)
                {
                    if (ReferenceEquals(l.Appointment, Ghost))
                    {
                        columnIndex = i;
                        col = l.Column;
                        cols = l.ColumnsInGroup;
                        break;
                    }
                }

                if (columnIndex >= 0)
                {
                    break;
                }
            }
        }

        if (columnIndex < 0)
        {
            return;
        }

        var colInfo = Columns[columnIndex];
        float x0 = contentX + (columnIndex * colW);
        DrawBlock(canvas, Ghost, colInfo.Accent, GhostStart.Value, GhostEnd.Value, col, cols, x0, colW, colInfo.DayStart, colInfo.DayStart.AddDays(1), isGhost: true);
    }

    private void DrawBlock(
        ICanvas canvas,
        Appointment a,
        Color? columnAccent,
        DateTime start,
        DateTime end,
        int col,
        int cols,
        float columnX,
        float columnW,
        DateTime dayStart,
        DateTime dayEnd,
        bool isGhost)
    {
        var clipStart = start < dayStart ? dayStart : start;
        var clipEnd = end > dayEnd ? dayEnd : end;
        float y1 = Scale.YForTime(clipStart - dayStart);
        float y2 = Scale.YForTime(clipEnd - dayStart);

        float slotW = columnW / Math.Max(1, cols);
        float x = columnX + (col * slotW) + 2;
        float rw = slotW - 4;
        float rh = MathF.Max(y2 - y1, 20);
        var rect = new RectF(x, y1, rw, rh);

        if (!isGhost)
        {
            hitMap.Add((a, rect));
        }

        float fontScale = Scale.HourHeight / 60f;
        float titleSize = 12 * fontScale;
        float titleBoxH = 16 * fontScale;
        float rangeSize = 10 * fontScale;
        float rangeBoxH = 14 * fontScale;

        var bg = a.Color ?? columnAccent ?? Theme.Accent;
        var bgSoft = new Color(bg.Red, bg.Green, bg.Blue, isGhost ? 0.35f : 0.18f);
        canvas.FillColor = bgSoft;
        canvas.FillRoundedRectangle(rect, 6);

        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(new RectF(x, y1, 3, rh), 1.5f);

        var textColor = new Color(bg.Red * 0.5f, bg.Green * 0.5f, bg.Blue * 0.5f);
        canvas.FontColor = textColor;
        canvas.FontSize = titleSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            a.Title ?? string.Empty,
            new RectF(x + 8, y1 + 2, rw - 10, MathF.Min(titleBoxH, rh - 4)),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > titleBoxH + rangeBoxH)
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = rangeSize;
            canvas.FontColor = Theme.Muted;
            var range = $"{FormatTime(start)} – {FormatTime(end)}";
            canvas.DrawString(
                range,
                new RectF(x + 8, y1 + titleBoxH + 2, rw - 10, rangeBoxH),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }

        if (!isGhost && rh >= 28)
        {
            var handle = new RectF(x + rw - 16, (y1 + rh) - 8, 12, 4);
            canvas.FillColor = textColor;
            canvas.Alpha = 0.5f;
            canvas.FillRoundedRectangle(handle, 2);
            canvas.Alpha = 1f;
            resizeHandles.Add((a, new RectF(x, (y1 + rh) - 12, rw, 14)));
        }
    }
}
