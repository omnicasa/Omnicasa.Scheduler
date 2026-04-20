using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Displays a single day of appointments on a time rail with horizontal swipe to change days and
/// pinch to zoom the time scale.
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

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly CarouselView carousel;

    private List<DateTime> dates = new List<DateTime>();

    private bool suppressSelect;

    private double pinchBase = 60;

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

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        GestureRecognizers.Add(pinch);

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

    /// <summary>Gets or sets the currently displayed date.</summary>
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

    /// <summary>Gets or sets the number of days to make swipable in each direction from the current date.</summary>
    public int DayWindow
    {
        get => (int)GetValue(DayWindowProperty);
        set => SetValue(DayWindowProperty, value);
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

    private void BuildDates()
    {
        var center = SelectedDate.Date;
        dates = new List<DateTime>((DayWindow * 2) + 1);
        for (int i = -DayWindow; i <= DayWindow; i++)
        {
            dates.Add(center.AddDays(i));
        }

        suppressSelect = true;
        carousel.ItemsSource = dates;
        carousel.CurrentItem = center;
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

        if (d.Date < dates[0].Date || d.Date > dates[^1].Date)
        {
            BuildDates();
            return;
        }

        if (carousel.CurrentItem is DateTime cur && cur.Date != d.Date)
        {
            suppressSelect = true;
            carousel.CurrentItem = d.Date;
            suppressSelect = false;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            pinchBase = HourHeight;
        }
        else if (e.Status == GestureStatus.Running)
        {
            HourHeight = Math.Clamp(pinchBase * e.Scale, 24, 200);
        }
    }

    private void RaiseHourHeightChanged() => HourHeightChanged?.Invoke();

    private void RaiseThemeChanged() => ThemeChanged?.Invoke();

    private void OnSourceChanged(IAppointmentSource? newSrc)
    {
        SourceChanged?.Invoke(newSrc);
    }
}
