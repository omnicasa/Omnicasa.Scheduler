using Android.Graphics;
using AGraphicsColor = Android.Graphics.Color;
using APaint = Android.Graphics.Paint;
using ARectF = Android.Graphics.RectF;

namespace Omnicasa.Schedule.Widget;

/// <summary>
/// Draws the ScheduleView day time-grid into an Android <see cref="Bitmap"/> for use in a widget
/// (RemoteViews can't host a custom view, so the standard trick is to render a bitmap and push it
/// into an <c>ImageView</c> via <c>SetImageViewBitmap</c>). Mirrors the look of the MAUI
/// <c>ScheduleViewRenderer</c>: hour grid + left rail, soft-fill blocks with an accent strip and
/// title/time, and the current-time line.
/// </summary>
public static class ScheduleWidgetRenderer
{
    // Colors matching ScheduleViewTheme defaults.
    private static readonly AGraphicsColor Background = AGraphicsColor.White;
    private static readonly AGraphicsColor Muted = AGraphicsColor.ParseColor("#8E8E93");
    private static readonly AGraphicsColor GridLine = AGraphicsColor.ParseColor("#E5E5EA");
    private static readonly AGraphicsColor Accent = AGraphicsColor.ParseColor("#FF3B30");
    private static readonly AGraphicsColor Today = AGraphicsColor.ParseColor("#FF3B30");

    /// <summary>
    /// Renders the schedule to a bitmap. <paramref name="widthPx"/>/<paramref name="heightPx"/> are
    /// the widget's pixel size; <paramref name="density"/> scales text/strokes for the screen.
    /// </summary>
    public static Bitmap Render(
        IEnumerable<WidgetAppointment> appointments,
        int widthPx,
        int heightPx,
        DateTime now,
        float density = 2f,
        int windowHours = 6)
    {
        widthPx = Math.Max(1, widthPx);
        heightPx = Math.Max(1, heightPx);

        var config = Bitmap.Config.Argb8888!;
        var bitmap = Bitmap.CreateBitmap(widthPx, heightPx, config)
                     ?? throw new InvalidOperationException("Bitmap.CreateBitmap returned null.");
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(Background);

        var window = ScheduleWindow.Build(appointments, now.Date, now, windowHours);

        float railWidth = 44f * density;
        float contentX = railWidth;
        float contentW = widthPx - contentX;
        if (contentW <= 0)
        {
            return bitmap;
        }

        float span = window.SpanMinutes;
        float YFor(int minute) => (minute - window.StartMinutes) / span * heightPx;

        using var line = new APaint { AntiAlias = true, StrokeWidth = 0.5f * density };
        line.Color = GridLine;
        using var text = new APaint(PaintFlags.AntiAlias) { Color = Muted, TextSize = 10f * density };

        int firstHour = window.StartMinutes / 60;
        int lastHour = window.EndMinutes / 60;
        for (int hour = firstHour; hour <= lastHour; hour++)
        {
            float y = YFor(hour * 60);
            canvas.DrawLine(contentX, y, widthPx, y, line);
            if (hour < 24)
            {
                text.Color = Muted;
                text.TextAlign = APaint.Align.Right;
                canvas.DrawText(HourLabel(hour), contentX - (6f * density), y + (4f * density), text);
            }
        }

        foreach (var item in window.Items)
        {
            DrawBlock(canvas, item, contentX, contentW, heightPx, density, YFor);
        }

        if (window.NowMinutes is { } nowMin && nowMin >= window.StartMinutes && nowMin <= window.EndMinutes)
        {
            float y = YFor(nowMin);
            using var marker = new APaint(PaintFlags.AntiAlias) { Color = Today, StrokeWidth = 1.5f * density };
            canvas.DrawCircle(contentX, y, 3f * density, marker);
            canvas.DrawLine(contentX, y, widthPx, y, marker);
        }

        return bitmap;
    }

    private static void DrawBlock(
        Canvas canvas,
        LaidOutWidgetItem item,
        float contentX,
        float contentW,
        int heightPx,
        float density,
        Func<int, float> yFor)
    {
        var dayStart = item.Start.Date;
        int startM = (int)(item.Start - dayStart).TotalMinutes;
        int endM = (int)(item.End - dayStart).TotalMinutes;
        float y1 = Math.Max(0, yFor(startM));
        float y2 = Math.Min(heightPx, yFor(endM));

        float slotW = contentW / Math.Max(1, item.ColumnsInGroup);
        float x = contentX + (item.Column * slotW) + (2f * density);
        float w = slotW - (4f * density);
        float h = Math.Max(y2 - y1, 16f * density);

        var bg = ParseColor(item.Appointment.BackgroundColor) ?? Accent;
        float radius = 6f * density;

        using var soft = new APaint(PaintFlags.AntiAlias) { Color = WithAlpha(bg, 46) };  // ~0.18
        canvas.DrawRoundRect(new ARectF(x, y1, x + w, y1 + h), radius, radius, soft);

        using var strip = new APaint(PaintFlags.AntiAlias) { Color = bg };
        float stripW = 3f * density;
        canvas.DrawRoundRect(new ARectF(x, y1, x + stripW, y1 + h), 1.5f * density, 1.5f * density, strip);

        var darker = AGraphicsColor.Rgb((int)(bg.R * 0.5f), (int)(bg.G * 0.5f), (int)(bg.B * 0.5f));
        using var title = new APaint(PaintFlags.AntiAlias)
        {
            Color = darker,
            TextSize = 11f * density,
            FakeBoldText = true,
        };
        float textX = x + (8f * density);
        if (h >= 16f * density)
        {
            canvas.DrawText(Ellipsize(item.Appointment.Title ?? string.Empty, w - (10f * density), title), textX, y1 + (12f * density), title);
        }

        if (h >= 32f * density)
        {
            using var range = new APaint(PaintFlags.AntiAlias) { Color = Muted, TextSize = 9f * density };
            string label = $"{TimeLabel(item.Start)} – {TimeLabel(item.End)}";
            canvas.DrawText(label, textX, y1 + (26f * density), range);
        }
    }

    private static AGraphicsColor WithAlpha(AGraphicsColor c, int alpha) => AGraphicsColor.Argb(alpha, c.R, c.G, c.B);

    private static AGraphicsColor? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            return AGraphicsColor.ParseColor(hex);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            return null;
        }
    }

    private static string Ellipsize(string value, float maxWidth, APaint paint)
    {
        if (paint.MeasureText(value) <= maxWidth || value.Length == 0)
        {
            return value;
        }

        const string ellipsis = "…";
        var trimmed = value;
        while (trimmed.Length > 1 && paint.MeasureText(trimmed + ellipsis) > maxWidth)
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        return trimmed + ellipsis;
    }

    private static string HourLabel(int hour) => hour switch
    {
        0 => "12 AM",
        12 => "12 PM",
        < 12 => $"{hour} AM",
        _ => $"{hour - 12} PM",
    };

    private static string TimeLabel(DateTime t)
        => t.ToString(t.Minute == 0 ? "h tt" : "h:mm tt", System.Globalization.CultureInfo.CurrentCulture).ToLowerInvariant();
}
