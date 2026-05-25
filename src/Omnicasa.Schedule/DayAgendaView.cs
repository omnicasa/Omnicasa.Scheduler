using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Displays one or more days of appointments on a shared time rail, with horizontal swipe between
/// pages and pinch to zoom the time scale.
/// </summary>
public class DayAgendaView : ContentView
{
    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleTheme),
            typeof(DayAgendaView),
            null,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).RaiseThemeChanged());

    /// <summary>Bindable property for <see cref="AppointmentSource"/>.</summary>
    public static readonly BindableProperty AppointmentSourceProperty =
        BindableProperty.Create(
            nameof(AppointmentSource),
            typeof(IAppointmentSource),
            typeof(DayAgendaView),
            null,
            propertyChanged: (b, _, n) => ((DayAgendaView)b).OnSourceChanged((IAppointmentSource?)n));

    /// <summary>Bindable property for <see cref="SelectedDate"/>.</summary>
    public static readonly BindableProperty SelectedDateProperty =
        BindableProperty.Create(
            nameof(SelectedDate),
            typeof(DateTime),
            typeof(DayAgendaView),
            DateTime.Today,
            propertyChanged: (b, _, n) => ((DayAgendaView)b).OnSelectedDateChanged((DateTime)n));

    /// <summary>Bindable property for <see cref="HourHeight"/>.</summary>
    public static readonly BindableProperty HourHeightProperty =
        BindableProperty.Create(
            nameof(HourHeight),
            typeof(double),
            typeof(DayAgendaView),
            60.0,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).RaiseHourHeightChanged());

    /// <summary>Bindable property for <see cref="DayWindow"/>.</summary>
    public static readonly BindableProperty DayWindowProperty =
        BindableProperty.Create(nameof(DayWindow), typeof(int), typeof(DayAgendaView), 365);

    /// <summary>Bindable property for <see cref="DaysPerPage"/>.</summary>
    public static readonly BindableProperty DaysPerPageProperty =
        BindableProperty.Create(
            nameof(DaysPerPage),
            typeof(int),
            typeof(DayAgendaView),
            1,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).OnDaysPerPageChangedInternal(),
            coerceValue: (_, v) => Math.Clamp((int)v, 1, 7));

    /// <summary>Bindable property for <see cref="FirstDayOfWeek"/>.</summary>
    public static readonly BindableProperty FirstDayOfWeekProperty =
        BindableProperty.Create(
            nameof(FirstDayOfWeek),
            typeof(DayOfWeek),
            typeof(DayAgendaView),
            DayOfWeek.Monday,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).OnFirstDayOfWeekChanged());

    /// <summary>Bindable property for <see cref="Persons"/>.</summary>
    public static readonly BindableProperty PersonsProperty =
        BindableProperty.Create(
            nameof(Persons),
            typeof(IList<IPerson>),
            typeof(DayAgendaView),
            null,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).OnPersonsChangedInternal());

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(DayAgendaRenderer),
            typeof(DayAgendaView),
            null,
            propertyChanged: (b, _, _) => ((DayAgendaView)b).RaiseRendererChanged());

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly CarouselView carousel;

    private List<DateTime> dates = new List<DateTime>();

    private bool suppressSelect;

    /// <summary>Initializes a new instance of the <see cref="DayAgendaView"/> class.</summary>
    public DayAgendaView()
    {
        carousel = new CarouselView
        {
            Loop = false,
            IsSwipeEnabled = true,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal)
            {
                SnapPointsAlignment = SnapPointsAlignment.Center,
                SnapPointsType = SnapPointsType.MandatorySingle,
            },
            PeekAreaInsets = new Thickness(0),
            ItemTemplate = new DataTemplate(() => new DayAgendaPage(this)),
        };
        carousel.CurrentItemChanged += OnCurrentItemChanged;
        Content = carousel;

        BuildDates();
    }

    /// <summary>Occurs when the user taps an appointment block.</summary>
    public event EventHandler<AppointmentEventArgs>? AppointmentTapped;

    /// <summary>Occurs when an appointment has been moved or resized by the user.</summary>
    public event EventHandler<AppointmentEventArgs>? AppointmentChanged;

    /// <summary>Internal: fired when <see cref="HourHeight"/> changes, for child pages.</summary>
    internal event Action? HourHeightChanged;

    /// <summary>Internal: fired when <see cref="Theme"/> changes, for child pages.</summary>
    internal event Action? ThemeChanged;

    /// <summary>Internal: fired when <see cref="AppointmentSource"/> changes, for child pages.</summary>
    internal event Action<IAppointmentSource?>? SourceChanged;

    /// <summary>Internal: fired when the shared scroll position changes, so other pages can align.</summary>
    internal event Action<double>? SharedScrollYChanged;

    /// <summary>Internal: fired when <see cref="DaysPerPage"/> changes, for child pages.</summary>
    internal event Action? DaysPerPageChanged;

    /// <summary>Internal: fired when <see cref="Persons"/> changes, for child pages.</summary>
    internal event Action? PersonsChanged;

    /// <summary>Internal: fired when <see cref="Renderer"/> changes, for child pages.</summary>
    internal event Action? RendererChanged;

    /// <summary>Gets the shared vertical scroll position across day pages, or NaN if uninitialized.</summary>
    internal double SharedScrollY { get; private set; } = double.NaN;

    /// <summary>Gets or sets the color theme.</summary>
    public ScheduleTheme Theme
    {
        get => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Gets or sets the appointment source.</summary>
    public IAppointmentSource? AppointmentSource
    {
        get => (IAppointmentSource?)GetValue(AppointmentSourceProperty);
        set => SetValue(AppointmentSourceProperty, value);
    }

    /// <summary>Gets or sets the currently displayed date (first day of the visible page).</summary>
    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>Gets or sets the height of one hour on the time rail, in logical pixels.</summary>
    public double HourHeight
    {
        get => (double)GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, Math.Clamp(value, 24, 200));
    }

    /// <summary>Gets or sets the number of days swipable in each direction from the anchor.</summary>
    public int DayWindow
    {
        get => (int)GetValue(DayWindowProperty);
        set => SetValue(DayWindowProperty, value);
    }

    /// <summary>Gets or sets the number of days shown side-by-side per page (1..7).</summary>
    public int DaysPerPage
    {
        get => (int)GetValue(DaysPerPageProperty);
        set => SetValue(DaysPerPageProperty, value);
    }

    /// <summary>Gets or sets the first day of the week, used for week-mode alignment (<see cref="DaysPerPage"/> == 7).</summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    /// <summary>Gets or sets the list of persons; when non-empty, each page shows a single day with one column per person.</summary>
    public IList<IPerson>? Persons
    {
        get => (IList<IPerson>?)GetValue(PersonsProperty);
        set => SetValue(PersonsProperty, value);
    }

    /// <summary>
    /// Custom painter for the agenda. Defaults to <see cref="DayAgendaRenderer.Default"/>. Subclass
    /// <see cref="DayAgendaRenderer"/> and override <c>DrawAppointment</c> for per-type appointment looks.
    /// </summary>
    public DayAgendaRenderer Renderer
    {
        get => (DayAgendaRenderer)GetValue(RendererProperty) ?? DayAgendaRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Enables or disables horizontal swiping between pages. Used by child pages during drag.</summary>
    /// <param name="enabled">True to allow swiping, false to lock the carousel.</param>
    internal void SetSwipeEnabled(bool enabled)
    {
        carousel.IsSwipeEnabled = enabled;
    }

    /// <summary>Sets the shared vertical scroll position and notifies pages.</summary>
    /// <param name="y">New scroll position in logical pixels.</param>
    internal void UpdateSharedScrollY(double y)
    {
        if (!double.IsNaN(SharedScrollY) && Math.Abs(y - SharedScrollY) < 0.5)
        {
            return;
        }

        SharedScrollY = y;
        SharedScrollYChanged?.Invoke(y);
    }

    /// <summary>Internal: raises <see cref="AppointmentTapped"/> from child pages.</summary>
    /// <param name="a">Tapped appointment.</param>
    internal void RaiseAppointmentTapped(Appointment a)
    {
        AppointmentTapped?.Invoke(this, new AppointmentEventArgs(a));
    }

    /// <summary>Internal: raises <see cref="AppointmentChanged"/> after a drag/resize commit.</summary>
    /// <param name="a">Modified appointment.</param>
    internal void RaiseAppointmentChanged(Appointment a)
    {
        AppointmentChanged?.Invoke(this, new AppointmentEventArgs(a));
    }

    private DateTime PageAnchor(DateTime date)
    {
        if (DaysPerPage == 7)
        {
            int delta = ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            return date.Date.AddDays(-delta);
        }

        return date.Date;
    }

    private void BuildDates()
    {
        var step = Math.Max(1, DaysPerPage);
        var anchor = PageAnchor(SelectedDate);
        var pagesEachSide = Math.Max(1, DayWindow / step);
        dates = new List<DateTime>((pagesEachSide * 2) + 1);
        for (int i = -pagesEachSide; i <= pagesEachSide; i++)
        {
            dates.Add(anchor.AddDays(i * step));
        }

        suppressSelect = true;
        carousel.ItemsSource = dates;
        carousel.CurrentItem = anchor;
        suppressSelect = false;
    }

    private void OnCurrentItemChanged(object? sender, CurrentItemChangedEventArgs e)
    {
        if (suppressSelect)
        {
            return;
        }

        if (e.CurrentItem is DateTime d && d.Date != SelectedDate.Date)
        {
            SelectedDate = d;
        }
    }

    private void OnSelectedDateChanged(DateTime d)
    {
        if (dates.Count == 0)
        {
            BuildDates();
            return;
        }

        var anchor = PageAnchor(d);
        if (anchor < dates[0].Date || anchor > dates[^1].Date)
        {
            BuildDates();
            return;
        }

        if (carousel.CurrentItem is DateTime cur && cur.Date != anchor)
        {
            suppressSelect = true;
            carousel.CurrentItem = anchor;
            suppressSelect = false;
        }
    }

    private void OnDaysPerPageChangedInternal()
    {
        DaysPerPageChanged?.Invoke();
        BuildDates();
    }

    private void OnFirstDayOfWeekChanged()
    {
        if (DaysPerPage == 7)
        {
            BuildDates();
        }
    }

    private void OnPersonsChangedInternal()
    {
        PersonsChanged?.Invoke();
        BuildDates();
    }

    private void RaiseHourHeightChanged() => HourHeightChanged?.Invoke();

    private void RaiseThemeChanged() => ThemeChanged?.Invoke();

    private void RaiseRendererChanged() => RendererChanged?.Invoke();

    private void OnSourceChanged(IAppointmentSource? newSrc)
    {
        SourceChanged?.Invoke(newSrc);
    }
}
