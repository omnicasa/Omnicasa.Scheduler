using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// One day per <see cref="ScheduleView"/>, swiped horizontally in a <see cref="CarouselView"/>.
/// Every page binds its <see cref="ScheduleView.VerticalOffset"/> and <see cref="ScheduleView.HourHeight"/>
/// to a single shared state object, so scrolling or pinch-zooming one page keeps the others at the
/// same boundary time when you swipe between them. Built entirely in code to keep the cross-page
/// bindings explicit (no compiled-binding plumbing to read through).
/// </summary>
public sealed class CarouselSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="CarouselSchedulePage"/> class.</summary>
    public CarouselSchedulePage()
    {
        Title = "Carousel (synced scroll)";

        var state = new SharedScheduleState();
        BindingContext = state;

        var carousel = new CarouselView
        {
            ItemsSource = state.Days,
            ItemTemplate = new DataTemplate(() => CreateDayView(state)),
        };

        Content = carousel;
    }

    private static ScheduleView CreateDayView(SharedScheduleState state)
    {
        var view = new ScheduleView
        {
            ViewMode = 1,
            ItemsSource = MainPage.Source.AllItems,
        };

        // Per-day range comes from the carousel item (a DaySlot).
        view.SetBinding(ScheduleView.StartDayProperty, new Binding(nameof(DaySlot.Start)));
        view.SetBinding(ScheduleView.EndDayProperty, new Binding(nameof(DaySlot.End)));

        // Shared, two-way: scroll/zoom on one page propagates to all the others.
        view.SetBinding(
            ScheduleView.VerticalOffsetProperty,
            new Binding(nameof(SharedScheduleState.SharedOffset), BindingMode.TwoWay, source: state));
        view.SetBinding(
            ScheduleView.HourHeightProperty,
            new Binding(nameof(SharedScheduleState.SharedHourHeight), BindingMode.TwoWay, source: state));

        return view;
    }
}

/// <summary>A single day shown by one carousel page.</summary>
public sealed class DaySlot
{
    /// <summary>Gets the day shown (also the inclusive end, so the view renders a single column).</summary>
    public DateTime Start { get; init; }

    /// <summary>Gets the inclusive end day (equal to <see cref="Start"/> for a one-day page).</summary>
    public DateTime End { get; init; }
}

/// <summary>Scroll position and zoom shared across every carousel page.</summary>
public sealed class SharedScheduleState : INotifyPropertyChanged
{
    private double sharedOffset;
    private double sharedHourHeight = 60;
    private double headerInset;

    /// <summary>Initializes a new instance of the <see cref="SharedScheduleState"/> class.</summary>
    public SharedScheduleState()
    {
        var today = DateTime.Today;
        for (int i = -7; i <= 7; i++)
        {
            var day = today.AddDays(i);
            Days.Add(new DaySlot { Start = day, End = day });
        }

        // Start every page near the current time (one hour of lead-in), clamped to midnight.
        double topHours = Math.Max(0, DateTime.Now.TimeOfDay.TotalHours - 1);
        sharedOffset = topHours * sharedHourHeight;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the days, one per carousel page.</summary>
    public ObservableCollection<DaySlot> Days { get; } = new ObservableCollection<DaySlot>();

    /// <summary>Gets or sets the shared vertical scroll offset (logical pixels), bound to every page.</summary>
    public double SharedOffset
    {
        get => sharedOffset;
        set => Set(ref sharedOffset, value);
    }

    /// <summary>Gets or sets the shared hour height (pixels), so pinch-zoom stays consistent across pages.</summary>
    public double SharedHourHeight
    {
        get => sharedHourHeight;
        set => Set(ref sharedHourHeight, value);
    }

    /// <summary>Gets or sets the top inset every page reserves under an overlaid glass header.</summary>
    public double HeaderInset
    {
        get => headerInset;
        set => Set(ref headerInset, value);
    }

    private void Set(ref double field, double value, [CallerMemberName] string? name = null)
    {
        if (Math.Abs(field - value) > 0.001)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
