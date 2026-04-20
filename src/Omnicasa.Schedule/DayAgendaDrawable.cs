using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Draws a day-view timeline with hour rail, event blocks, and the current-time indicator.
/// </summary>
public sealed class DayAgendaDrawable : IDrawable
{
    private readonly List<(Appointment Item, RectF Rect)> hitMap = new List<(Appointment, RectF)>();

    private readonly List<(Appointment Item, RectF Handle)> resizeHandles = new List<(Appointment, RectF)>();

    /// <summary>Gets or sets the day being rendered.</summary>
    public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Gets or sets the color theme.</summary>
    public ScheduleTheme Theme { get; set; } = new ScheduleTheme();

    /// <summary>Gets or sets the vertical scale mapping times to Y coordinates.</summary>
    public TimeScale Scale { get; set; } = new TimeScale(60);

    /// <summary>Gets or sets the laid-out appointments to render.</summary>
    public IReadOnlyList<LaidOutAppointment> Appointments { get; set; } = Array.Empty<LaidOutAppointment>();

    /// <summary>Gets or sets the all-day appointments (drawn in the banner area).</summary>
    public IReadOnlyList<Appointment> AllDay { get; set; } = Array.Empty<Appointment>();

    /// <summary>Gets or sets the width of the time rail on the left, in logical pixels.</summary>
    public float TimeRailWidth { get; set; } = 56;

    /// <summary>Gets or sets the height of the all-day banner, in logical pixels.</summary>
    public float AllDayBannerHeight { get; set; }

    /// <summary>Gets or sets the current time to highlight (only drawn when the day matches <see cref="Day"/>).</summary>
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

        canvas.FillColor = Theme.Background;
        canvas.FillRectangle(0, 0, w, h);

        float contentX = TimeRailWidth;
        float contentW = w - TimeRailWidth;

        canvas.StrokeColor = Theme.GridLine;
        canvas.StrokeSize = 0.5f;
        canvas.FontColor = Theme.Muted;
        canvas.FontSize = 11;
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

                canvas.DrawString(label, 4, y - 8, TimeRailWidth - 8, 16, HorizontalAlignment.Right, VerticalAlignment.Top);
            }
        }

        foreach (var laid in Appointments)
        {
            var a = laid.Appointment;
            DrawBlock(canvas, a, a.Start, a.End, laid.Column, laid.ColumnsInGroup, contentX, contentW, isGhost: false);
        }

        if (Ghost is not null && GhostStart is not null && GhostEnd is not null)
        {
            int col = 0;
            int cols = 1;
            foreach (var l in Appointments)
            {
                if (ReferenceEquals(l.Appointment, Ghost))
                {
                    col = l.Column;
                    cols = l.ColumnsInGroup;
                    break;
                }
            }

            DrawBlock(canvas, Ghost, GhostStart.Value, GhostEnd.Value, col, cols, contentX, contentW, isGhost: true);
        }

        if (Now is { } now && DateOnly.FromDateTime(now) == Day)
        {
            float y = Scale.YForTime(now.TimeOfDay);
            canvas.FillColor = Theme.Today;
            canvas.FillCircle(contentX, y, 4);
            canvas.StrokeColor = Theme.Today;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(contentX, y, w, y);
        }
    }

    private static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }

    private void DrawBlock(
        ICanvas canvas,
        Appointment a,
        DateTime start,
        DateTime end,
        int col,
        int cols,
        float contentX,
        float contentW,
        bool isGhost)
    {
        float y1 = Scale.YForTime(start.TimeOfDay);
        float y2 = Scale.YForTime(end.TimeOfDay);
        if (end.Date > start.Date)
        {
            y2 = Scale.YForTime(TimeSpan.FromHours(24));
        }

        float colW = contentW / Math.Max(1, cols);
        float x = contentX + (col * colW) + 2;
        float rw = colW - 4;
        float rh = MathF.Max(y2 - y1, 20);
        var rect = new RectF(x, y1, rw, rh);

        if (!isGhost)
        {
            hitMap.Add((a, rect));
        }

        var bg = a.Color ?? Theme.Accent;
        var bgSoft = new Color(bg.Red, bg.Green, bg.Blue, isGhost ? 0.35f : 0.18f);
        canvas.FillColor = bgSoft;
        canvas.FillRoundedRectangle(rect, 6);

        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(new RectF(x, y1, 3, rh), 1.5f);

        var textColor = new Color(bg.Red * 0.5f, bg.Green * 0.5f, bg.Blue * 0.5f);
        canvas.FontColor = textColor;
        canvas.FontSize = 12;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(
            a.Title ?? string.Empty,
            new RectF(x + 8, y1 + 2, rw - 10, MathF.Min(16, rh - 4)),
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        if (rh > 30)
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = 10;
            canvas.FontColor = Theme.Muted;
            var range = $"{FormatTime(start)} – {FormatTime(end)}";
            canvas.DrawString(
                range,
                new RectF(x + 8, y1 + 18, rw - 10, 14),
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
