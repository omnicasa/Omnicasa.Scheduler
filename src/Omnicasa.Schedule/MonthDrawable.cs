using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Lays out a month grid (header, weekday row, day cells) and delegates the painting of each
/// primitive to a <see cref="MonthRenderer"/>. Layout and hit-testing live here; the look is the
/// renderer's. Used inside the year calendar and the full-size month calendar.
/// </summary>
public sealed class MonthDrawable : IDrawable
{
    private readonly Dictionary<DateOnly, RectF> hitMap = new Dictionary<DateOnly, RectF>();

    /// <summary>Gets or sets the month to render (the day component is ignored).</summary>
    public DateOnly Month { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Gets or sets the color theme (and optional fonts) used while drawing.</summary>
    public ScheduleTheme Theme { get; set; } = new ScheduleTheme();

    /// <summary>Gets or sets the painter; defaults to the built-in look.</summary>
    public MonthRenderer Renderer { get; set; } = MonthRenderer.Default;

    /// <summary>Gets or sets a callback that returns the number of events on a given date.</summary>
    public Func<DateOnly, int>? CountProvider { get; set; }

    /// <summary>Gets or sets the first day of the week (defaults to the current culture).</summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Gets or sets a value indicating whether to draw the month header.</summary>
    public bool ShowHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to render compactly (the small year-grid look).
    /// When <see langword="false"/>, fonts and density dots scale up for a full-size month view.
    /// </summary>
    public bool Compact { get; set; } = true;

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
        float headerH = ShowHeader ? (Compact ? MathF.Min(22, h * 0.14f) : MathF.Min(40, h * 0.12f)) : 0;

        if (ShowHeader)
        {
            float headerSize = (float)(Theme.MonthHeaderFontSize ?? (headerH * 0.8f));
            Renderer.DrawHeader(new MonthHeaderContext
            {
                Canvas = canvas,
                Theme = Theme,
                Rect = new RectF(2, 0, w, headerH),
                Text = new DateTime(Month.Year, Month.Month, 1).ToString("MMM", CultureInfo.CurrentCulture),
                FontSize = headerSize,
                Font = ResolveFont(bold: true),
            });
        }

        float gridTop = headerH + 2;
        float cellW = w / 7f;
        float dayRowH = (h - gridTop) / 7f;

        float weekdaySize = (float)(Theme.WeekdayFontSize ?? (Compact ? MathF.Min(9, dayRowH * 0.38f) : MathF.Min(15, dayRowH * 0.32f)));
        var weekdayFont = ResolveFont(bold: false);
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)FirstDayOfWeek + i) % 7);
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow];
            var letter = string.IsNullOrEmpty(name) ? "?" : name[..1];
            Renderer.DrawWeekday(new MonthWeekdayContext
            {
                Canvas = canvas,
                Theme = Theme,
                Rect = new RectF(i * cellW, gridTop, cellW, dayRowH),
                Text = letter,
                DayOfWeek = dow,
                FontSize = weekdaySize,
                Font = weekdayFont,
            });
        }

        gridTop += dayRowH;

        var first = new DateOnly(Month.Year, Month.Month, 1);
        int offset = (((int)first.DayOfWeek - (int)FirstDayOfWeek) + 7) % 7;
        int daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);
        float daySize = (float)(Theme.DayNumberFontSize ?? (Compact ? MathF.Min(12, dayRowH * 0.48f) : MathF.Min(22, dayRowH * 0.42f)));

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

            Renderer.DrawDay(new MonthDayContext
            {
                Canvas = canvas,
                Theme = Theme,
                Rect = rect,
                Date = date,
                IsToday = isToday,
                EventCount = CountProvider?.Invoke(date) ?? 0,
                TextColor = textColor,
                FontSize = daySize,
                Font = ResolveFont(bold: isToday),
                Compact = Compact,
            });
        }
    }

    private IFont ResolveFont(bool bold)
    {
        if (string.IsNullOrEmpty(Theme.FontFamily))
        {
            return bold ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
        }

        return new Microsoft.Maui.Graphics.Font(Theme.FontFamily, bold ? FontWeights.Bold : FontWeights.Normal);
    }
}
