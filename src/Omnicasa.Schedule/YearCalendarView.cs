using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Displays a scrollable year-at-a-glance calendar with 12 months per year and event-density dots.
/// </summary>
public class YearCalendarView : ContentView
{
    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleTheme),
            typeof(YearCalendarView),
            null,
            propertyChanged: (b, _, _) => ((YearCalendarView)b).InvalidateAllMonths());

    /// <summary>Bindable property for <see cref="AppointmentSource"/>.</summary>
    public static readonly BindableProperty AppointmentSourceProperty =
        BindableProperty.Create(
            nameof(AppointmentSource),
            typeof(IAppointmentSource),
            typeof(YearCalendarView),
            null,
            propertyChanged: (b, o, n) => ((YearCalendarView)b).OnSourceChanged((IAppointmentSource?)o, (IAppointmentSource?)n));

    /// <summary>Bindable property for <see cref="MinYear"/>.</summary>
    public static readonly BindableProperty MinYearProperty =
        BindableProperty.Create(
            nameof(MinYear),
            typeof(int),
            typeof(YearCalendarView),
            DateTime.Today.Year - 5,
            propertyChanged: (b, _, _) => ((YearCalendarView)b).Rebuild());

    /// <summary>Bindable property for <see cref="MaxYear"/>.</summary>
    public static readonly BindableProperty MaxYearProperty =
        BindableProperty.Create(
            nameof(MaxYear),
            typeof(int),
            typeof(YearCalendarView),
            DateTime.Today.Year + 5,
            propertyChanged: (b, _, _) => ((YearCalendarView)b).Rebuild());

    /// <summary>Bindable property for <see cref="InitialYear"/>.</summary>
    public static readonly BindableProperty InitialYearProperty =
        BindableProperty.Create(
            nameof(InitialYear),
            typeof(int),
            typeof(YearCalendarView),
            DateTime.Today.Year);

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(MonthRenderer),
            typeof(YearCalendarView),
            null,
            propertyChanged: (b, _, _) => ((YearCalendarView)b).ApplyRenderer());

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly ScrollView scroll;

    private readonly VerticalStackLayout stack;

    private readonly Dictionary<DateOnly, int> counts = new Dictionary<DateOnly, int>();

    private readonly List<Grid> yearBlocks = new List<Grid>();

    private CancellationTokenSource? loadCts;

    /// <summary>Initializes a new instance of the <see cref="YearCalendarView"/> class.</summary>
    public YearCalendarView()
    {
        stack = new VerticalStackLayout
        {
            Spacing = 24,
            Padding = new Thickness(0, 16, 0, 32),
        };
        scroll = new ScrollView
        {
            Content = stack,
            Orientation = ScrollOrientation.Vertical,
        };
        Content = scroll;
        Rebuild();
        Loaded += (_, _) => ScrollToYear(InitialYear, animated: false);
    }

    /// <summary>Occurs when the user taps a day cell inside any month.</summary>
    public event EventHandler<DayTappedEventArgs>? DayTapped;

    /// <summary>Gets or sets the color theme used while rendering.</summary>
    public ScheduleTheme Theme
    {
        get => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Gets or sets the appointment source used to compute event-density dots.</summary>
    public IAppointmentSource? AppointmentSource
    {
        get => (IAppointmentSource?)GetValue(AppointmentSourceProperty);
        set => SetValue(AppointmentSourceProperty, value);
    }

    /// <summary>Gets or sets the earliest year rendered (inclusive).</summary>
    public int MinYear
    {
        get => (int)GetValue(MinYearProperty);
        set => SetValue(MinYearProperty, value);
    }

    /// <summary>Gets or sets the latest year rendered (inclusive).</summary>
    public int MaxYear
    {
        get => (int)GetValue(MaxYearProperty);
        set => SetValue(MaxYearProperty, value);
    }

    /// <summary>Gets or sets the year initially scrolled into view on first load.</summary>
    public int InitialYear
    {
        get => (int)GetValue(InitialYearProperty);
        set => SetValue(InitialYearProperty, value);
    }

    /// <summary>Gets or sets the painter for each month grid; defaults to the built-in look.</summary>
    public MonthRenderer Renderer
    {
        get => (MonthRenderer)GetValue(RendererProperty) ?? MonthRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Scrolls the view to the specified year.</summary>
    /// <param name="year">Target year.</param>
    /// <param name="animated">Whether the scroll should animate.</param>
    public async void ScrollToYear(int year, bool animated = true)
    {
        var block = yearBlocks.Find(g => g.BindingContext is int y && y == year);
        if (block is null)
        {
            return;
        }

        await scroll.ScrollToAsync(block, ScrollToPosition.Start, animated);
    }

    // Runs on a background thread: fetch appointments and tally per-day counts, clamped to the
    // requested window so one long/all-day event can't blow the day-by-day loop out of bounds.
    private static async Task<Dictionary<DateOnly, int>> BuildCounts(IAppointmentSource source, DateTime from, DateTime to, CancellationToken ct)
    {
        var list = await source.GetAsync(from, to, ct).ConfigureAwait(false);
        var lo = DateOnly.FromDateTime(from);
        var hi = DateOnly.FromDateTime(to);
        var map = new Dictionary<DateOnly, int>();
        foreach (var a in list)
        {
            ct.ThrowIfCancellationRequested();
            var d0 = DateOnly.FromDateTime(a.Start);
            var d1 = DateOnly.FromDateTime(a.End);
            if (d1 < d0)
            {
                d1 = d0;
            }

            if (d0 < lo)
            {
                d0 = lo;
            }

            if (d1 > hi)
            {
                d1 = hi;
            }

            for (var d = d0; d <= d1; d = d.AddDays(1))
            {
                map[d] = map.TryGetValue(d, out var c) ? c + 1 : 1;
            }
        }

        return map;
    }

    private void Rebuild()
    {
        stack.Clear();
        yearBlocks.Clear();
        for (int y = MinYear; y <= MaxYear; y++)
        {
            stack.Add(BuildYearBlock(y));
        }

        _ = RefreshCountsAsync();
    }

    private View BuildYearBlock(int year)
    {
        var grid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            Padding = new Thickness(16, 0),
            BindingContext = year,
        };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int r = 0; r < 3; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        }

        for (int c = 0; c < 4; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        var header = new Label
        {
            Text = year.ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = 34,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Accent,
            Margin = new Thickness(0, 0, 0, 4),
        };
        Grid.SetRow(header, 0);
        Grid.SetColumn(header, 0);
        Grid.SetColumnSpan(header, 4);
        grid.Add(header);

        for (int m = 1; m <= 12; m++)
        {
            var mv = new MonthGraphicsView
            {
                Theme = Theme,
                CountProvider = GetCount,
                Renderer = Renderer,
                HeightRequest = 150,
            };
            mv.SetMonth(year, m);
            mv.DayTapped += (_, e) => DayTapped?.Invoke(this, e);
            int idx = m - 1;
            int row = (idx / 4) + 1;
            int col = idx % 4;
            Grid.SetRow(mv, row);
            Grid.SetColumn(mv, col);
            grid.Add(mv);
        }

        yearBlocks.Add(grid);
        return grid;
    }

    private int GetCount(DateOnly d) => counts.TryGetValue(d, out var c) ? c : 0;

    private async Task RefreshCountsAsync()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        if (AppointmentSource is null)
        {
            counts.Clear();
            InvalidateAllMonths();
            return;
        }

        var source = AppointmentSource;
        var from = new DateTime(MinYear, 1, 1);
        var to = new DateTime(MaxYear, 12, 31, 23, 59, 59);
        try
        {
            // Fetch and expand off the UI thread: a wide year range can mean thousands of day-by-day
            // increments that would otherwise freeze the frame when the source binding first fires.
            var fresh = await Task.Run(() => BuildCounts(source, from, to, ct), ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            counts.Clear();
            foreach (var kv in fresh)
            {
                counts[kv.Key] = kv.Value;
            }

            InvalidateAllMonths();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void InvalidateAllMonths()
    {
        foreach (var mv in MonthViews())
        {
            mv.Theme = Theme;
            mv.Invalidate();
        }
    }

    private void ApplyRenderer()
    {
        foreach (var mv in MonthViews())
        {
            mv.Renderer = Renderer;
        }
    }

    private IEnumerable<MonthGraphicsView> MonthViews()
    {
        foreach (var block in yearBlocks)
        {
            foreach (var child in block.Children)
            {
                if (child is MonthGraphicsView mv)
                {
                    yield return mv;
                }
            }
        }
    }

    private void OnSourceChanged(IAppointmentSource? oldSrc, IAppointmentSource? newSrc)
    {
        if (oldSrc is not null)
        {
            oldSrc.Changed -= OnSourceDataChanged;
        }

        if (newSrc is not null)
        {
            newSrc.Changed += OnSourceDataChanged;
        }

        _ = RefreshCountsAsync();
    }

    private void OnSourceDataChanged(object? sender, AppointmentsChangedEventArgs e) => _ = RefreshCountsAsync();
}
