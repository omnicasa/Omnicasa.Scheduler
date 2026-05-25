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

    /// <summary>Gets or sets the painter; defaults to the built-in look.</summary>
    public DayAgendaRenderer Renderer { get; set; } = DayAgendaRenderer.Default;

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

        Renderer.DrawBackground(canvas, dirtyRect, Theme);

        float contentX = TimeRailWidth;
        float contentW = w - TimeRailWidth;
        float colW = contentW / n;
        float fontScale = Scale.HourHeight / 60f;

        Renderer.DrawHeader(canvas, contentX, colW, n, fontScale, Columns, Theme, HeaderHeight);
        Renderer.DrawHourGrid(canvas, w, contentX, fontScale, Scale, Theme, TimeRailWidth);

        for (int i = 0; i < n; i++)
        {
            float x0 = contentX + (i * colW);
            DrawColumnEvents(canvas, i, x0, colW);
        }

        Renderer.DrawColumnSeparators(canvas, contentX, colW, n, h, HeaderHeight, Theme);
        if (Now is { } now)
        {
            Renderer.DrawTodayMarker(canvas, contentX, colW, n, Columns, now, Scale, Theme);
        }

        DrawGhost(canvas, contentX, colW, n);

        Drawn?.Invoke();
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

        bool showResizeHandle = !isGhost && rh >= 28;
        if (showResizeHandle)
        {
            resizeHandles.Add((a, new RectF(x, (y1 + rh) - 12, rw, 14)));
        }

        Renderer.DrawAppointment(new DayAgendaAppointmentContext
        {
            Canvas = canvas,
            Item = a,
            Rect = rect,
            BlockColor = a.Color ?? columnAccent ?? Theme.Accent,
            Theme = Theme,
            FontScale = Scale.HourHeight / 60f,
            DisplayStart = start,
            DisplayEnd = end,
            IsGhost = isGhost,
            ShowResizeHandle = showResizeHandle,
        });
    }
}
