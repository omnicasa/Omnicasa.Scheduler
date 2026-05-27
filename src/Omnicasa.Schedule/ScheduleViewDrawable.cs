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

    /// <summary>
    /// Scale applied to the typing block about its center (1 = full size), driving the bubble
    /// show/dismiss animation. Values may briefly exceed 1 for the spring overshoot.
    /// </summary>
    public float TypingScale { get; set; } = 1f;

    /// <summary>Optional held item drawn as a floating, draggable block.</summary>
    public IScheduleItem? HoldingItem { get; set; }

    /// <summary>Column the held block is being dragged over; -1 means draw at its natural position.</summary>
    public int HoldingDragColumn { get; set; } = -1;

    /// <summary>Start time of the held block while dragging; null means draw at its natural start.</summary>
    public DateTime? HoldingDragStart { get; set; }

    /// <summary>End time of the held block while dragging; null means draw at its natural end.</summary>
    public DateTime? HoldingDragEnd { get; set; }
}

/// <summary>Renders the sticky header bar (day groups + per-column sub-headers) above <see cref="ScheduleView"/>'s body.</summary>
public sealed class ScheduleHeaderDrawable : IDrawable
{
    /// <summary>Shared render state.</summary>
    public ScheduleRenderContext Context { get; set; } = new ScheduleRenderContext();

    /// <summary>Painter used for the header; defaults to the built-in look.</summary>
    public ScheduleViewRenderer Renderer { get; set; } = ScheduleViewRenderer.Default;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        Renderer.DrawBackground(canvas, dirtyRect, Context.Theme);
        Renderer.DrawHeader(canvas, dirtyRect, Context);
    }
}

/// <summary>Renders the scrollable body (time rail + hour grid + appointment blocks + today line).</summary>
public sealed class ScheduleBodyDrawable : IDrawable
{
    private readonly List<(IScheduleItem Item, RectF Rect)> hitMap = new List<(IScheduleItem, RectF)>();

    private RectF? typingRect;

    private RectF? holdingRect;

    /// <summary>Shared render state.</summary>
    public ScheduleRenderContext Context { get; set; } = new ScheduleRenderContext();

    /// <summary>Painter used for the body; defaults to the built-in look.</summary>
    public ScheduleViewRenderer Renderer { get; set; } = ScheduleViewRenderer.Default;

    /// <summary>Hit map populated after every <see cref="Draw"/>.</summary>
    public IReadOnlyList<(IScheduleItem Item, RectF Rect)> HitMap => hitMap;

    /// <summary>Rect of the rendered typing item, if any (populated by <see cref="Draw"/>).</summary>
    public RectF? TypingRect => typingRect;

    /// <summary>Rect of the rendered holding item, if any (populated by <see cref="Draw"/>).</summary>
    public RectF? HoldingRect => holdingRect;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        hitMap.Clear();
        typingRect = null;
        holdingRect = null;

        var ctx = Context;
        var theme = ctx.Theme;
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        int n = ctx.Columns.Count;

        Renderer.DrawBackground(canvas, dirtyRect, theme);

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

        Renderer.DrawHourGrid(canvas, w, contentX, ctx);

        for (int i = 0; i < n; i++)
        {
            float x0 = contentX + (i * colW);
            DrawColumnItems(canvas, i, x0, colW);
        }

        Renderer.DrawColumnSeparators(canvas, contentX, colW, n, h, theme);
        Renderer.DrawTodayMarker(canvas, contentX, colW, ctx);
        DrawTypingItem(canvas, contentX, colW);
        DrawHoldingItem(canvas, contentX, colW);
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

        Renderer.DrawAppointment(new ScheduleAppointmentContext
        {
            Canvas = canvas,
            Item = item,
            Rect = rect,
            BlockColor = item.Color ?? columnAccent ?? theme.Accent,
            Theme = theme,
        });
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

        // Bubble animation: scale about the center and fade, both driven by TypingScale.
        float ts = Context.TypingScale;
        bool animating = MathF.Abs(ts - 1f) > 0.001f;
        var drawRect = rect;
        if (animating)
        {
            float dw = rect.Width * ts;
            float dh = rect.Height * ts;
            drawRect = new RectF(rect.Center.X - (dw / 2f), rect.Center.Y - (dh / 2f), dw, dh);
            canvas.SaveState();
            canvas.Alpha = Math.Clamp(ts, 0f, 1f);
        }

        Renderer.DrawTypingItem(new ScheduleTypingContext
        {
            Canvas = canvas,
            Item = typing,
            Rect = drawRect,
            BlockColor = typing.Color ?? column.Accent ?? theme.Accent,
            Theme = theme,
        });

        if (animating)
        {
            canvas.RestoreState();
        }
    }

    private int FindTypingColumn(ITypingScheduleItem typing)
        => FindColumn(DateOnly.FromDateTime(typing.Start), typing.PersonId);

    // Column whose day matches; prefers the one matching personId, else the first of that day. -1 if none.
    private int FindColumn(DateOnly date, string? personId)
    {
        int matchByDayOnly = -1;
        for (int i = 0; i < Context.Columns.Count; i++)
        {
            var col = Context.Columns[i];
            if (DateOnly.FromDateTime(col.DayStart) != date)
            {
                continue;
            }

            if (string.Equals(col.PersonId, personId, StringComparison.Ordinal))
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

    private void DrawHoldingItem(ICanvas canvas, float contentX, float colW)
    {
        var item = Context.HoldingItem;
        if (item is null || item.IsAllDay)
        {
            return;
        }

        // Column: the one being dragged over, else the item's natural column.
        int colIdx = Context.HoldingDragColumn >= 0
            ? Context.HoldingDragColumn
            : FindColumn(DateOnly.FromDateTime(item.Start), item.PersonId);
        if (colIdx < 0 || colIdx >= Context.Columns.Count)
        {
            return;
        }

        var column = Context.Columns[colIdx];
        var dayStart = column.DayStart;
        var dayEnd = dayStart.AddDays(1);

        // Start/end: the dragged times (already on this column's day) while dragging, else natural.
        var startTime = Context.HoldingDragStart ?? item.Start;
        var endTime = Context.HoldingDragEnd ?? item.End;
        if (endTime <= startTime)
        {
            endTime = startTime.AddMinutes(30);
        }

        var clipStart = startTime < dayStart ? dayStart : startTime;
        var clipEnd = endTime > dayEnd ? dayEnd : endTime;
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
        holdingRect = rect;

        Renderer.DrawHoldingItem(new ScheduleHoldingContext
        {
            Canvas = canvas,
            Item = item,
            Rect = rect,
            DisplayStart = startTime,
            DisplayEnd = endTime,
            BlockColor = item.Color ?? column.Accent ?? theme.Accent,
            Theme = theme,
            IsDragging = Context.HoldingDragColumn >= 0,
        });
    }
}
