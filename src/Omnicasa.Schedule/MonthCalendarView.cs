using System.Globalization;
using Microsoft.Maui.Controls;

namespace Omnicasa.Schedule;

/// <summary>
/// Displays full-size months stacked vertically with continuous scrolling (one month per block,
/// "MMMM yyyy" header + a full month grid). Sits between <see cref="YearCalendarView"/> and the
/// day view in a year → month → day drill-down. Reports day taps via <see cref="DayTapped"/>.
/// </summary>
public class MonthCalendarView : ContentView
{
    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleTheme),
            typeof(MonthCalendarView),
            null,
            propertyChanged: (b, _, _) => ((MonthCalendarView)b).Rebuild());

    /// <summary>Bindable property for <see cref="AppointmentSource"/>.</summary>
    public static readonly BindableProperty AppointmentSourceProperty =
        BindableProperty.Create(
            nameof(AppointmentSource),
            typeof(IAppointmentSource),
            typeof(MonthCalendarView),
            null,
            propertyChanged: (b, o, n) => ((MonthCalendarView)b).OnSourceChanged((IAppointmentSource?)o, (IAppointmentSource?)n));

    /// <summary>Bindable property for <see cref="MinYear"/>.</summary>
    public static readonly BindableProperty MinYearProperty =
        BindableProperty.Create(
            nameof(MinYear),
            typeof(int),
            typeof(MonthCalendarView),
            DateTime.Today.Year - 5,
            propertyChanged: (b, _, _) => ((MonthCalendarView)b).Rebuild());

    /// <summary>Bindable property for <see cref="MaxYear"/>.</summary>
    public static readonly BindableProperty MaxYearProperty =
        BindableProperty.Create(
            nameof(MaxYear),
            typeof(int),
            typeof(MonthCalendarView),
            DateTime.Today.Year + 5,
            propertyChanged: (b, _, _) => ((MonthCalendarView)b).Rebuild());

    /// <summary>Bindable property for <see cref="InitialDate"/>.</summary>
    public static readonly BindableProperty InitialDateProperty =
        BindableProperty.Create(
            nameof(InitialDate),
            typeof(DateOnly),
            typeof(MonthCalendarView),
            DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(MonthRenderer),
            typeof(MonthCalendarView),
            null,
            propertyChanged: (b, _, _) => ((MonthCalendarView)b).ApplyRenderer());

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly ScrollView scroll;

    private readonly VerticalStackLayout stack;

    private readonly Dictionary<DateOnly, int> counts = new Dictionary<DateOnly, int>();

    private readonly Dictionary<int, View> monthBlocks = new Dictionary<int, View>();

    private CancellationTokenSource? loadCts;

    private bool initialScrollDone;

    /// <summary>Initializes a new instance of the <see cref="MonthCalendarView"/> class.</summary>
    public MonthCalendarView()
    {
        stack = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(16, 0),
        };
        scroll = new ScrollView
        {
            Content = stack,
            Orientation = ScrollOrientation.Vertical,
        };
        Content = scroll;
        Rebuild();

        // Size each month block to the viewport so exactly one month fills the screen, and do the
        // initial scroll once a real height is known.
        SizeChanged += (_, _) => ApplyOneMonthPerScreen();
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

    /// <summary>Gets or sets the month initially scrolled into view on first load.</summary>
    public DateOnly InitialDate
    {
        get => (DateOnly)GetValue(InitialDateProperty);
        set => SetValue(InitialDateProperty, value);
    }

    /// <summary>Gets or sets the painter for each month grid; defaults to the built-in look.</summary>
    public MonthRenderer Renderer
    {
        get => (MonthRenderer)GetValue(RendererProperty) ?? MonthRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Scrolls so the given month is at the top of the viewport.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="animated">Whether the scroll should animate.</param>
    public async void ScrollToMonth(int year, int month, bool animated = true)
    {
        if (monthBlocks.TryGetValue(Key(year, month), out var block))
        {
            await scroll.ScrollToAsync(block, ScrollToPosition.Start, animated);
        }
    }

    private static int Key(int year, int month) => (year * 12) + (month - 1);

    private void Rebuild()
    {
        stack.Clear();
        monthBlocks.Clear();
        for (int y = MinYear; y <= MaxYear; y++)
        {
            for (int m = 1; m <= 12; m++)
            {
                stack.Add(BuildMonthBlock(y, m));
            }
        }

        ApplyOneMonthPerScreen();
        _ = RefreshCountsAsync();
    }

    private View BuildMonthBlock(int year, int month)
    {
        var block = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };

        var title = new Label
        {
            Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            FontSize = Theme.MonthHeaderFontSize ?? 22,
            FontFamily = Theme.FontFamily,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Accent,
            Margin = new Thickness(2, 12, 0, 4),
        };
        Grid.SetRow(title, 0);
        block.Add(title);

        var mv = new MonthGraphicsView
        {
            Theme = Theme,
            Compact = false,
            ShowHeader = false,
            CountProvider = GetCount,
            Renderer = Renderer,
            VerticalOptions = LayoutOptions.Fill,
        };
        mv.SetMonth(year, month);
        mv.DayTapped += (_, e) => DayTapped?.Invoke(this, e);
        Grid.SetRow(mv, 1);
        block.Add(mv);

        monthBlocks[Key(year, month)] = block;
        return block;
    }

    // One month per screen: each block fills the viewport height (continuous vertical scroll).
    private void ApplyOneMonthPerScreen()
    {
        // An unbounded measure (e.g. hosted in an Auto row or vertical stack) reports an absurd
        // height; sizing blocks to it would explode the scroll content into CALayer NaN territory.
        if (Height <= 0 || !double.IsFinite(Height) || Height > 10000)
        {
            return;
        }

        foreach (var block in monthBlocks.Values)
        {
            block.HeightRequest = Height;
        }

        if (!initialScrollDone)
        {
            initialScrollDone = true;

            // Defer so the new block heights are applied before the scroll offset is measured.
            Dispatcher.Dispatch(() => ScrollToMonth(InitialDate.Year, InitialDate.Month, animated: false));
        }
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

        try
        {
            var from = new DateTime(MinYear, 1, 1);
            var to = new DateTime(MaxYear, 12, 31, 23, 59, 59);
            var list = await AppointmentSource.GetAsync(from, to, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            counts.Clear();
            foreach (var a in list)
            {
                var d0 = DateOnly.FromDateTime(a.Start);
                var d1 = DateOnly.FromDateTime(a.End);
                if (d1 < d0)
                {
                    d1 = d0;
                }

                for (var d = d0; d <= d1; d = d.AddDays(1))
                {
                    counts[d] = counts.TryGetValue(d, out var c) ? c + 1 : 1;
                }
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
        foreach (var block in monthBlocks.Values)
        {
            if (block is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    if (child is MonthGraphicsView mv)
                    {
                        yield return mv;
                    }
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
