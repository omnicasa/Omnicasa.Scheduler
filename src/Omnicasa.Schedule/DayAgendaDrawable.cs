using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Draws a day- or multi-day timeline with hour rail, event blocks, and the current-time indicator.
/// </summary>
public sealed class DayAgendaDrawable : IDrawable
{
    private readonly List<(Appointment Item, RectF Rect)> hitMap = new List<(Appointment, RectF)>();

    private readonly List<(Appointment Item, RectF Handle)> resizeHandles = new List<(Appointment, RectF)>();

    /// <summary>Gets or sets the days rendered as columns, left-to-right.</summary>
    public IReadOnlyList<DateOnly> Days { get; set; } = new[] { DateOnly.FromDateTime(DateTime.Today) };

    /// <summary>Gets or sets the color theme.</summary>
    public ScheduleTheme Theme { get; set; } = new ScheduleTheme();

    /// <summary>Gets or sets the vertical scale mapping times to Y coordinates.</summary>
    public TimeScale Scale { get; set; } = new TimeScale(60);

    /// <summary>Gets or sets per-day laid-out appointments; outer index matches <see cref="Days"/>.</summary>
    public IReadOnlyList<IReadOnlyList<LaidOutAppointment>> ColumnsByDay { get; set; }
        = Array.Empty<IReadOnlyList<LaidOutAppointment>>();

    /// <summary>Gets or sets per-day all-day appointments; outer index matches <see cref="Days"/>.</summary>
    public IReadOnlyList<IReadOnlyList<Appointment>> AllDayByDay { get; set; }
        = Array.Empty<IReadOnlyList<Appointment>>();

    /// <summary>Gets or sets the width of the time rail on the left, in logical pixels.</summary>
    public float TimeRailWidth { get; set; } = 56;

    /// <summary>Gets or sets the header height (column day labels). Zero hides the header.</summary>
    public float HeaderHeight { get; set; }

    /// <summary>Gets or sets the current time to highlight.</summary>
    public DateTime? Now { get; set; }

    /// <summary>Gets or sets the appointment being dragged, drawn as a translucent ghost.</summary>
    public Appointment? Ghost { get; set; }

    /// <summary>Gets or sets the ghost start time used during drag operations.</summary>
    public DateTime? GhostStart { get; set; }

    /// <summary>Gets or sets the ghost end time used during drag operations.</summary>
    public DateTime? GhostEnd { get; set; }

    /// <summary>Gets the hit-test map populated after <see cref="Draw"/>.</summary>
    public IReadOnlyList<(Appointment Item, RectF Rect)> HitMap => hitMap;

    /// <summary>Gets the resize-handle rectangles populated after <see cref="Draw"/>.</summary>
    public IReadOnlyList<(Appointment Item, RectF Handle)> ResizeHandles => resizeHandles;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        hitMap.Clear();
        resizeHandles.Clear();

        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        int n = Math.Max(1, Days.Count);

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
    }

    private static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }

    private void DrawColumnHeaders(ICanvas canvas, float contentX, float colW, int n, float fontScale)
    {
        if (HeaderHeight <= 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        float dowSize = 11 * fontScale;
        float daySize = 18 * fontScale;
        for (int i = 0; i < n; i++)
        {
            float x0 = contentX + (i * colW);
            var day = Days[i];
            bool isToday = day == today;

            if (isToday)
            {
                canvas.FillColor = new Color(Theme.Today.Red, Theme.Today.Green, Theme.Today.Blue, 0.1f);
                canvas.FillRectangle(x0, 0, colW, HeaderHeight);
            }

            canvas.FontColor = isToday ? Theme.Today : Theme.Muted;
            canvas.FontSize = dowSize;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            var dow = day.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
            canvas.DrawString(dow, x0, 4, colW, dowSize + 2, HorizontalAlignment.Center, VerticalAlignment.Top);

            canvas.FontColor = isToday ? Theme.Today : Theme.Foreground;
            canvas.FontSize = daySize;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            canvas.DrawString(
                day.Day.ToString(CultureInfo.InvariantCulture),
                x0,
                4 + dowSize + 2,
                colW,
                daySize + 2,
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

    private void DrawColumnEvents(ICanvas canvas, int dayIndex, float x0, float colW)
    {
        if (dayIndex >= ColumnsByDay.Count)
        {
            return;
        }

        var day = Days[dayIndex];
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        foreach (var laid in ColumnsByDay[dayIndex])
        {
            var a = laid.Appointment;
            DrawBlock(canvas, a, a.Start, a.End, laid.Column, laid.ColumnsInGroup, x0, colW, dayStart, dayEnd, isGhost: false);
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
        int col = -1;
        for (int i = 0; i < n; i++)
        {
            if (Days[i] == today)
            {
                col = i;
                break;
            }
        }

        if (col < 0)
        {
            return;
        }

        float y = Scale.YForTime(now.TimeOfDay);
        float x0 = contentX + (col * colW);
        float x1 = x0 + colW;
        canvas.FillColor = Theme.Today;
        canvas.FillCircle(x0, y, 4);
        canvas.StrokeColor = Theme.Today;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(x0, y, x1, y);
    }

    private void DrawGhost(ICanvas canvas, float contentX, float colW, int n)
    {
        if (Ghost is null || GhostStart is null || GhostEnd is null)
        {
            return;
        }

        var gStart = GhostStart.Value;
        int col = 0;
        int cols = 1;
        int dayIndex = 0;
        for (int i = 0; i < n; i++)
        {
            if (Days[i] == DateOnly.FromDateTime(gStart))
            {
                dayIndex = i;
                break;
            }
        }

        if (dayIndex < ColumnsByDay.Count)
        {
            foreach (var l in ColumnsByDay[dayIndex])
            {
                if (ReferenceEquals(l.Appointment, Ghost))
                {
                    col = l.Column;
                    cols = l.ColumnsInGroup;
                    break;
                }
            }
        }

        float x0 = contentX + (dayIndex * colW);
        var dayStart = Days[dayIndex].ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        DrawBlock(canvas, Ghost, gStart, GhostEnd.Value, col, cols, x0, colW, dayStart, dayEnd, isGhost: true);
    }

    private void DrawBlock(
        ICanvas canvas,
        Appointment a,
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

        var bg = a.Color ?? Theme.Accent;
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
