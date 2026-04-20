using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// A <see cref="GraphicsView"/> that renders a single month grid and reports day taps.
/// </summary>
public class MonthGraphicsView : GraphicsView
{
    private readonly MonthDrawable drawable = new MonthDrawable();

    /// <summary>Initializes a new instance of the <see cref="MonthGraphicsView"/> class.</summary>
    public MonthGraphicsView()
    {
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);
    }

    /// <summary>Occurs when the user taps a day cell.</summary>
    public event EventHandler<DayTappedEventArgs>? DayTapped;

    /// <summary>Gets the year currently shown.</summary>
    public int YearNumber => drawable.Month.Year;

    /// <summary>Gets the month (1–12) currently shown.</summary>
    public int MonthNumber => drawable.Month.Month;

    /// <summary>Gets or sets the color theme.</summary>
    public ScheduleTheme Theme
    {
        get => drawable.Theme;
        set
        {
            drawable.Theme = value;
            Invalidate();
        }
    }

    /// <summary>Gets or sets the event-count provider used to draw density dots.</summary>
    public Func<DateOnly, int>? CountProvider
    {
        get => drawable.CountProvider;
        set
        {
            drawable.CountProvider = value;
            Invalidate();
        }
    }

    /// <summary>Sets the year and month displayed by the view.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    public void SetMonth(int year, int month)
    {
        drawable.Month = new DateOnly(year, month, 1);
        Invalidate();
    }

    /// <summary>Refreshes the highlighted "today" cell to the current system date.</summary>
    public void RefreshToday()
    {
        drawable.Today = DateOnly.FromDateTime(DateTime.Today);
        Invalidate();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var pt = e.GetPosition(this);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        foreach (var kv in drawable.HitMap)
        {
            if (kv.Value.Contains(p))
            {
                DayTapped?.Invoke(this, new DayTappedEventArgs(kv.Key));
                return;
            }
        }
    }
}
