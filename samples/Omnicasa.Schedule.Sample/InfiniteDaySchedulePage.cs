using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// One day per <see cref="ScheduleView"/> in a <see cref="CarouselView"/> that scrolls left/right
/// forever: whenever the user swipes within a few pages of either edge of the loaded window, another
/// chunk of days is appended (or prepended, re-anchoring <see cref="CarouselView.Position"/> so the
/// visible day doesn't move). Scroll offset and pinch-zoom stay in sync across pages via the same
/// shared-state pattern as <see cref="CarouselSchedulePage"/>.
/// </summary>
public sealed class InfiniteDaySchedulePage : ContentPage
{
    // Days added per extension; also the initial radius around today.
    private const int ChunkSize = 10;

    // Extend as soon as the user is within this many pages of an edge.
    private const int EdgeThreshold = 3;

    private readonly ObservableCollection<DaySlot> days = new ObservableCollection<DaySlot>();

    private readonly CarouselView carousel;

    // True while a chunk is being spliced in, so re-entrant PositionChanged events are ignored.
    private bool extending;

    /// <summary>Initializes a new instance of the <see cref="InfiniteDaySchedulePage"/> class.</summary>
    public InfiniteDaySchedulePage()
    {
        var today = DateTime.Today;
        for (int i = -ChunkSize; i <= ChunkSize; i++)
        {
            var day = today.AddDays(i);
            days.Add(new DaySlot { Start = day, End = day });
        }

        // Shared offset/zoom only; this page keeps its own sliding day window instead of state.Days.
        var state = new SharedScheduleState();

        carousel = new CarouselView
        {
            Loop = false,
            ItemsSource = days,
            ItemTemplate = new DataTemplate(() => CreateDayView(state)),
        };
        carousel.PositionChanged += OnPositionChanged;
        carousel.CurrentItemChanged += OnCurrentItemChanged;

        Content = carousel;

        // Land on today (the middle of the seeded window).
        carousel.Position = ChunkSize;
        Title = FormatTitle(today);
    }

    private static ScheduleView CreateDayView(SharedScheduleState state)
    {
        var view = new ScheduleView
        {
            ViewMode = 1,
            ItemsSource = MainPage.Source.AllItems,
        };

        view.SetBinding(ScheduleView.StartDayProperty, new Binding(nameof(DaySlot.Start)));
        view.SetBinding(ScheduleView.EndDayProperty, new Binding(nameof(DaySlot.End)));

        view.SetBinding(
            ScheduleView.VerticalOffsetProperty,
            new Binding(nameof(SharedScheduleState.SharedOffset), BindingMode.TwoWay, source: state));
        view.SetBinding(
            ScheduleView.HourHeightProperty,
            new Binding(nameof(SharedScheduleState.SharedHourHeight), BindingMode.TwoWay, source: state));

        return view;
    }

    private static string FormatTitle(DateTime day) => day.ToString("ddd d MMM yyyy");

    private void OnCurrentItemChanged(object? sender, CurrentItemChangedEventArgs e)
    {
        if (e.CurrentItem is DaySlot slot)
        {
            Title = FormatTitle(slot.Start);
        }
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (extending)
        {
            return;
        }

        if (e.CurrentPosition >= days.Count - 1 - EdgeThreshold)
        {
            extending = true;

            // Deferred so the ItemsSource never mutates mid-swipe layout.
            Dispatcher.Dispatch(() =>
            {
                var last = days[days.Count - 1].Start;
                for (int i = 1; i <= ChunkSize; i++)
                {
                    var day = last.AddDays(i);
                    days.Add(new DaySlot { Start = day, End = day });
                }

                extending = false;
            });
        }
        else if (e.CurrentPosition <= EdgeThreshold)
        {
            extending = true;

            Dispatcher.Dispatch(() =>
            {
                var first = days[0].Start;
                for (int i = 1; i <= ChunkSize; i++)
                {
                    var day = first.AddDays(-i);
                    days.Insert(0, new DaySlot { Start = day, End = day });
                }

                // Prepending shifts every index; re-anchor so the visible day doesn't jump.
                carousel.Position += ChunkSize;
                extending = false;
            });
        }
    }
}
