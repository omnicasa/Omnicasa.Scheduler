using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>Paints the month title ("MMM") for <see cref="MonthDrawable"/>.</summary>
public sealed class MonthHeaderContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>Active theme (colors + optional fonts).</summary>
    public ScheduleTheme Theme { get; init; } = new ScheduleTheme();

    /// <summary>Box the header occupies.</summary>
    public RectF Rect { get; init; }

    /// <summary>Header text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }

    /// <summary>Resolved font.</summary>
    public IFont Font { get; init; } = Microsoft.Maui.Graphics.Font.DefaultBold;
}

/// <summary>Paints one weekday-letter heading cell for <see cref="MonthDrawable"/>.</summary>
public sealed class MonthWeekdayContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>Active theme.</summary>
    public ScheduleTheme Theme { get; init; } = new ScheduleTheme();

    /// <summary>Cell the weekday letter occupies.</summary>
    public RectF Rect { get; init; }

    /// <summary>Weekday letter to draw.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Which weekday this cell represents.</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }

    /// <summary>Resolved font.</summary>
    public IFont Font { get; init; } = Microsoft.Maui.Graphics.Font.Default;
}

/// <summary>Paints one day cell (number, today highlight, density dot) for <see cref="MonthDrawable"/>.</summary>
public sealed class MonthDayContext
{
    /// <summary>Target canvas.</summary>
    public ICanvas Canvas { get; init; } = null!;

    /// <summary>Active theme.</summary>
    public ScheduleTheme Theme { get; init; } = new ScheduleTheme();

    /// <summary>Cell the day occupies.</summary>
    public RectF Rect { get; init; }

    /// <summary>The date this cell represents.</summary>
    public DateOnly Date { get; init; }

    /// <summary>True when this is the highlighted "today" cell.</summary>
    public bool IsToday { get; init; }

    /// <summary>Number of events on this date (drives the density dot).</summary>
    public int EventCount { get; init; }

    /// <summary>Resolved text color (today / weekend / weekday already applied).</summary>
    public Color TextColor { get; init; } = Colors.Black;

    /// <summary>Resolved font size.</summary>
    public float FontSize { get; init; }

    /// <summary>Resolved font (bold for today).</summary>
    public IFont Font { get; init; } = Microsoft.Maui.Graphics.Font.Default;

    /// <summary>Whether the compact (year-grid) sizing is in effect; affects dot size.</summary>
    public bool Compact { get; init; }
}

/// <summary>
/// Pluggable painter for a month grid (used by <see cref="MonthGraphicsView"/>, and therefore by
/// <see cref="MonthCalendarView"/> and <see cref="YearCalendarView"/>). Subclass and override only
/// the primitives you need; each default reproduces the built-in look. The most common override is
/// <see cref="DrawDay"/> (custom day cells). Layout and hit-testing stay in <see cref="MonthDrawable"/>.
/// </summary>
public class MonthRenderer
{
    /// <summary>Shared default renderer used when none is supplied.</summary>
    public static MonthRenderer Default { get; } = new MonthRenderer();

    /// <summary>Draws the month title.</summary>
    public virtual void DrawHeader(MonthHeaderContext ctx)
    {
        var canvas = ctx.Canvas;
        canvas.FontColor = ctx.Theme.Accent;
        canvas.FontSize = ctx.FontSize;
        canvas.Font = ctx.Font;
        canvas.DrawString(ctx.Text, ctx.Rect.X, ctx.Rect.Y, ctx.Rect.Width, ctx.Rect.Height, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    /// <summary>Draws one weekday-letter heading.</summary>
    public virtual void DrawWeekday(MonthWeekdayContext ctx)
    {
        var canvas = ctx.Canvas;
        canvas.FontColor = ctx.Theme.Muted;
        canvas.FontSize = ctx.FontSize;
        canvas.Font = ctx.Font;
        canvas.DrawString(ctx.Text, ctx.Rect, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    /// <summary>Draws one day cell. Override and switch on <see cref="MonthDayContext.Date"/> for custom looks.</summary>
    public virtual void DrawDay(MonthDayContext ctx)
    {
        var canvas = ctx.Canvas;
        var rect = ctx.Rect;

        if (ctx.IsToday)
        {
            float r = MathF.Min(rect.Width, rect.Height) * 0.38f;
            canvas.FillColor = ctx.Theme.Today;
            canvas.FillCircle(rect.Center.X, rect.Center.Y, r);
        }

        canvas.FontColor = ctx.TextColor;
        canvas.FontSize = ctx.FontSize;
        canvas.Font = ctx.Font;
        canvas.DrawString(ctx.Date.Day.ToString(CultureInfo.CurrentCulture), rect, HorizontalAlignment.Center, VerticalAlignment.Center);

        if (ctx.EventCount > 0)
        {
            canvas.FillColor = ctx.IsToday ? Colors.White : ctx.Theme.Accent;
            float dotY = rect.Bottom - (ctx.Compact ? 3 : 7);
            canvas.FillCircle(rect.Center.X, dotY, ctx.Compact ? 1.4f : 3f);
        }
    }
}
