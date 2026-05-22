using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>A column rendered by <see cref="ScheduleView"/> (a day, or a day-person pair).</summary>
public sealed class ScheduleViewColumn
{
    /// <summary>Day this column represents (midnight).</summary>
    public DateTime DayStart { get; set; }

    /// <summary>Top-row header text (e.g. "MON 24").</summary>
    public string HeaderPrimary { get; set; } = string.Empty;

    /// <summary>Optional bottom-row header text (day number or person initials).</summary>
    public string? HeaderSecondary { get; set; }

    /// <summary>Optional column accent (person color).</summary>
    public Color? Accent { get; set; }

    /// <summary>True when this column's day equals <see cref="DateTime.Today"/>.</summary>
    public bool IsToday { get; set; }

    /// <summary>Optional person id used to match a typing item to this column when in persons mode.</summary>
    public string? PersonId { get; set; }

    /// <summary>Items rendered in this column (after overlap layout).</summary>
    public IReadOnlyList<LaidOutItem> Items { get; set; } = Array.Empty<LaidOutItem>();
}

/// <summary>Shared rendering state used by the header and body drawables of <see cref="ScheduleView"/>.</summary>
public sealed class ScheduleRenderContext
{
    /// <summary>Columns drawn left-to-right after the time rail.</summary>
    public IReadOnlyList<ScheduleViewColumn> Columns { get; set; } = Array.Empty<ScheduleViewColumn>();

    /// <summary>Theme bundle (colors + font sizes).</summary>
    public ScheduleViewTheme Theme { get; set; } = new ScheduleViewTheme();

    /// <summary>Vertical scale mapping times to canvas Y coordinates within the body canvas (no header padding).</summary>
    public TimeScale Scale { get; set; } = new TimeScale(60);

    /// <summary>Width of the left time-rail column, in logical pixels.</summary>
    public float TimeRailWidth { get; set; } = 56;

    /// <summary>Header bar height; 0 means no header.</summary>
    public float HeaderHeight { get; set; }

    /// <summary>Optional "now" timestamp used to draw the today marker.</summary>
    public DateTime? Now { get; set; }

    /// <summary>Optional draft / typing item rendered as a shadowed overlay over the body.</summary>
    public ITypingScheduleItem? TypingItem { get; set; }
}

/// <summary>Renders the sticky header bar (day groups + per-column sub-headers) above <see cref="ScheduleView"/>'s body.</summary>
public sealed class ScheduleHeaderDrawable : IDrawable
{
    /// <summary>Shared render state.</summary>
    public ScheduleRenderContext Context { get; set; } = new ScheduleRenderContext();

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var ctx = Context;
        var theme = ctx.Theme;
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        int n = ctx.Columns.Count;

        canvas.FillColor = theme.Background;
        canvas.FillRectangle(0, 0, w, h);

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
}

/// <summary>Renders the scrollable body (time rail + hour grid + appointment blocks + today line).</summary>
public sealed class ScheduleBodyDrawable : IDrawable
{
    private readonly List<(IScheduleItem Item, RectF Rect)> hitMap = new List<(IScheduleItem, RectF)>();

    private RectF? typingRect;

    /// <summary>Shared render state.</summary>
    public ScheduleRenderContext Context { get; set; } = new ScheduleRenderContext();

    /// <summary>Hit map populated after every <see cref="Draw"/>.</summary>
    public IReadOnlyList<(IScheduleItem Item, RectF Rect)> HitMap => hitMap;

    /// <summary>Rect of the rendered typing item, if any (populated by <see cref="Draw"/>).</summary>
    public RectF? TypingRect => typingRect;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        hitMap.Clear();
        typingRect = null;

        var ctx = Context;
        var theme = ctx.Theme;
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        int n = ctx.Columns.Count;

        canvas.FillColor = theme.Background;
        canvas.FillRectangle(0, 0, w, h);

        if (n == 0)
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

        DrawHourGridAndLabels(canvas, w, contentX);

        for (int i = 0; i < n; i++)
        {
            float x0 = contentX + (i * colW);
            DrawColumnItems(canvas, i, x0, colW);
        }

        DrawColumnSeparators(canvas, contentX, colW, n, h);
        DrawTodayMarker(canvas, contentX, colW, n);
        DrawTypingItem(canvas, contentX, colW);
    }

    private static string FormatTime(DateTime t)
    {
        return t.Minute == 0
            ? t.ToString("h tt", CultureInfo.CurrentCulture).ToLowerInvariant()
            : t.ToString("h:mm tt", CultureInfo.CurrentCulture).ToLowerInvariant();
    }

    private void DrawHourGridAndLabels(ICanvas canvas, float w, float contentX)
    {
        var theme = Context.Theme;
        var scale = Context.Scale;
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
            canvas.DrawLine(contentX, y, w, y);
            if (hr >= 24)
            {
                continue;
            }

            string label = hr switch
            {
                0 => "12 AM",
                12 => "12 PM",
                < 12 => $"{hr} AM",
                _ => $"{hr - 12} PM",
            };

            canvas.DrawString(
                label,
                4,
                y - (labelBoxH / 2f),
                Context.TimeRailWidth - 8,
                labelBoxH,
                HorizontalAlignment.Right,
                VerticalAlignment.Top);
        }
    }

    private void DrawColumnItems(ICanvas canvas, int columnIndex, float x0, float colW)
    {
        var column = Context.Columns[columnIndex];
        var dayStart = column.DayStart;
        var dayEnd = dayStart.AddDays(1);

        foreach (var laid in column.Items)
        {
            DrawBlock(canvas, laid.Item, column.Accent, laid.Column, laid.ColumnsInGroup, x0, colW, dayStart, dayEnd);
        }
    }

    private void DrawColumnSeparators(ICanvas canvas, float contentX, float colW, int n, float h)
    {
        if (n <= 1)
        {
            return;
        }

        canvas.StrokeColor = Context.Theme.GridLine;
        canvas.StrokeSize = 0.5f;
        for (int i = 1; i < n; i++)
        {
            float x = contentX + (i * colW);
            canvas.DrawLine(x, 0, x, h);
        }
    }

    private void DrawTodayMarker(ICanvas canvas, float contentX, float colW, int n)
    {
        if (Context.Now is not { } now)
        {
            return;
        }

        var today = DateOnly.FromDateTime(now);
        for (int i = 0; i < n; i++)
        {
            var col = Context.Columns[i];
            if (DateOnly.FromDateTime(col.DayStart) != today)
            {
                continue;
            }

            float y = Context.Scale.YForTime(now.TimeOfDay);
            float x0 = contentX + (i * colW);
            float x1 = x0 + colW;
            var markerColor = col.Accent ?? Context.Theme.Today;
            canvas.FillColor = markerColor;
            canvas.FillCircle(x0, y, 4);
            canvas.StrokeColor = markerColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(x0, y, x1, y);
        }
    }

    private void DrawBlock(
        ICanvas canvas,
        IScheduleItem item,
        Color? columnAccent,
        int col,
        int cols,
        float columnX,
        float columnW,
        DateTime dayStart,
        DateTime dayEnd)
    {
        var theme = Context.Theme;
        var scale = Context.Scale;

        var clipStart = item.Start < dayStart ? dayStart : item.Start;
        var clipEnd = item.End > dayEnd ? dayEnd : item.End;
        float y1 = scale.YForTime(clipStart - dayStart);
        float y2 = scale.YForTime(clipEnd - dayStart);

        float slotW = columnW / Math.Max(1, cols);
        float x = columnX + (col * slotW) + 2;
        float rw = slotW - 4;
        float rh = MathF.Max(y2 - y1, 20);
        var rect = new RectF(x, y1, rw, rh);
        hitMap.Add((item, rect));

        float titleSize = (float)theme.BlockTitleFontSize;
        float titleBoxH = titleSize + 4f;
        float rangeSize = (float)theme.BlockRangeFontSize;
        float rangeBoxH = rangeSize + 4f;

        var bg = item.Color ?? columnAccent ?? theme.Accent;
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

    private void DrawTypingItem(ICanvas canvas, float contentX, float colW)
    {
        var typing = Context.TypingItem;
        if (typing is null || typing.IsAllDay)
        {
            return;
        }

        int colIdx = FindTypingColumn(typing);
        if (colIdx < 0)
        {
            return;
        }

        var column = Context.Columns[colIdx];
        var dayStart = column.DayStart;
        var dayEnd = dayStart.AddDays(1);
        var clipStart = typing.Start < dayStart ? dayStart : typing.Start;
        var clipEnd = typing.End > dayEnd ? dayEnd : typing.End;
        if (clipEnd <= clipStart)
        {
            return;
        }

        var scale = Context.Scale;
        var theme = Context.Theme;
        float y1 = scale.YForTime(clipStart - dayStart);
        float y2 = scale.YForTime(clipEnd - dayStart);
        float x = contentX + (colIdx * colW) + 4;
        float rw = colW - 8;
        float rh = MathF.Max(y2 - y1, 24);
        var rect = new RectF(x, y1, rw, rh);
        typingRect = rect;

        var bg = typing.Color ?? column.Accent ?? theme.Accent;

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

    private int FindTypingColumn(ITypingScheduleItem typing)
    {
        var typingDate = DateOnly.FromDateTime(typing.Start);
        int matchByDayOnly = -1;
        for (int i = 0; i < Context.Columns.Count; i++)
        {
            var col = Context.Columns[i];
            if (DateOnly.FromDateTime(col.DayStart) != typingDate)
            {
                continue;
            }

            if (string.Equals(col.PersonId, typing.PersonId, StringComparison.Ordinal))
            {
                return i;
            }

            if (matchByDayOnly < 0)
            {
                matchByDayOnly = i;
            }
        }

        return matchByDayOnly;
    }
}
