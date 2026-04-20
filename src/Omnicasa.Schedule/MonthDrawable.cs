using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Draws a compact month grid (used inside the year calendar view) onto an <see cref="ICanvas"/>.
/// </summary>
public sealed class MonthDrawable : IDrawable
{
    private readonly Dictionary<DateOnly, RectF> hitMap = new Dictionary<DateOnly, RectF>();

    /// <summary>Gets or sets the month to render (the day component is ignored).</summary>
    public DateOnly Month { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Gets or sets the color theme used while drawing.</summary>
    public ScheduleTheme Theme { get; set; } = new ScheduleTheme();

    /// <summary>Gets or sets a callback that returns the number of events on a given date.</summary>
    public Func<DateOnly, int>? CountProvider { get; set; }

    /// <summary>Gets or sets the first day of the week (defaults to the current culture).</summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Gets or sets a value indicating whether to draw the month header.</summary>
    public bool ShowHeader { get; set; } = true;

    /// <summary>Gets or sets the date to highlight as "today" (or null to disable highlighting).</summary>
    public DateOnly? Today { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Gets the hit-test map populated after <see cref="Draw"/>.</summary>
    public IReadOnlyDictionary<DateOnly, RectF> HitMap => hitMap;

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        hitMap.Clear();
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        float headerH = ShowHeader ? MathF.Min(22, h * 0.14f) : 0;

        if (ShowHeader)
        {
            canvas.FontColor = Theme.Accent;
            canvas.FontSize = headerH * 0.8f;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            var label = new DateTime(Month.Year, Month.Month, 1).ToString("MMM", CultureInfo.CurrentCulture);
            canvas.DrawString(label, 2, 0, w, headerH, HorizontalAlignment.Left, VerticalAlignment.Center);
        }

        float gridTop = headerH + 2;
        float cellW = w / 7f;
        float rowH = (h - gridTop) / 7f;
        float dayRowH = rowH;

        canvas.FontColor = Theme.Muted;
        canvas.FontSize = MathF.Min(9, dayRowH * 0.38f);
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)FirstDayOfWeek + i) % 7);
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow];
            var letter = string.IsNullOrEmpty(name) ? "?" : name[..1];
            canvas.DrawString(letter, i * cellW, gridTop, cellW, dayRowH, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        gridTop += dayRowH;

        var first = new DateOnly(Month.Year, Month.Month, 1);
        int offset = (((int)first.DayOfWeek - (int)FirstDayOfWeek) + 7) % 7;
        int daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);

        for (int d = 1; d <= daysInMonth; d++)
        {
            int idx = (d - 1) + offset;
            int row = idx / 7;
            int col = idx % 7;
            var rect = new RectF(col * cellW, gridTop + (row * dayRowH), cellW, dayRowH);
            var date = new DateOnly(Month.Year, Month.Month, d);
            hitMap[date] = rect;

            bool isToday = Today == date;
            Color textColor;
            if (isToday)
            {
                textColor = Colors.White;
            }
            else if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                textColor = Theme.Saturday;
            }
            else if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                textColor = Theme.Sunday;
            }
            else
            {
                textColor = Theme.Foreground;
            }

            if (isToday)
            {
                float r = MathF.Min(rect.Width, rect.Height) * 0.38f;
                canvas.FillColor = Theme.Today;
                canvas.FillCircle(rect.Center.X, rect.Center.Y, r);
            }

            canvas.FontColor = textColor;
            canvas.FontSize = MathF.Min(12, dayRowH * 0.48f);
            canvas.Font = isToday ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
            canvas.DrawString(d.ToString(CultureInfo.CurrentCulture), rect, HorizontalAlignment.Center, VerticalAlignment.Center);

            int count = CountProvider?.Invoke(date) ?? 0;
            if (count > 0)
            {
                canvas.FillColor = isToday ? Colors.White : Theme.Accent;
                float dotY = rect.Bottom - 3;
                canvas.FillCircle(rect.Center.X, dotY, 1.4f);
            }
        }
    }
}
