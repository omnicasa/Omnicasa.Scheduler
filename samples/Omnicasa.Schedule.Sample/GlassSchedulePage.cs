using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// iOS 26 "liquid glass" style schedule: the day pages are full-bleed and scroll under a single
/// pinned <see cref="ScheduleHeaderView"/> with a translucent blur background. Each page runs
/// <see cref="ScheduleHeaderMode.Linked"/> (no in-house header); swiping the carousel re-points
/// the shared header at the current page, and <see cref="ScheduleView.TopContentInset"/> keeps
/// hour 0 visible below the glass bar.
/// </summary>
public sealed class GlassSchedulePage : ContentPage
{
    // Same trio as SchedulePage; the sample source tags its appointments with p1..p3.
    private static readonly IList<IPerson> DemoPersons = new List<IPerson>
    {
        new Person { Id = "p1", Name = "Alice Murphy", Color = Color.FromArgb("#007AFF") },
        new Person { Id = "p2", Name = "Bob Reyes", Color = Color.FromArgb("#34C759") },
        new Person { Id = "p3", Name = "Charlie Mendes", Color = Color.FromArgb("#FF9500") },
    };

    private readonly Dictionary<DaySlot, ScheduleView> viewsBySlot = new Dictionary<DaySlot, ScheduleView>();

    private readonly SharedScheduleState state;

    private readonly ScheduleHeaderView header;

    private readonly CarouselView carousel;

    /// <summary>Initializes a new instance of the <see cref="GlassSchedulePage"/> class.</summary>
    public GlassSchedulePage()
    {
        Title = "Glass header";

        state = new SharedScheduleState();
        BindingContext = state;

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Persons",
            Command = new Command(() => state.Persons = state.Persons is null ? DemoPersons : null),
        });

        header = new ScheduleHeaderView
        {
            VerticalOptions = LayoutOptions.Start,
            HeaderBackground = CreateGlassBackground(),
        };

        // Whatever height the header settles on (day bar + all-day lanes) becomes the body inset.
        header.SizeChanged += (_, _) =>
        {
            if (header.Height > 0)
            {
                state.HeaderInset = header.Height;
            }
        };

        carousel = new CarouselView
        {
            ItemsSource = state.Days,
            ItemTemplate = new DataTemplate(() => CreateDayView()),
            Loop = false,
        };
        carousel.CurrentItemChanged += (_, e) => RelinkHeader(e.CurrentItem as DaySlot);

        // Start on today (the state lists a fortnight centered on it).
        carousel.CurrentItem = state.Days[state.Days.Count / 2];

        Content = new Grid
        {
            Children = { carousel, header },
        };
    }

    private static View CreateGlassBackground()
    {
#if IOS
        // System blur material stands in for glass; on an iOS 26 SDK swap the effect for UIGlassEffect.
        var host = new ContentView();
        host.HandlerChanged += (_, _) =>
        {
            if (host.Handler?.PlatformView is UIKit.UIView native
                && !native.Subviews.OfType<UIKit.UIVisualEffectView>().Any())
            {
                var blur = new UIKit.UIVisualEffectView(
                    UIKit.UIBlurEffect.FromStyle(UIKit.UIBlurEffectStyle.SystemChromeMaterial))
                {
                    Frame = native.Bounds,
                    AutoresizingMask = UIKit.UIViewAutoresizing.FlexibleWidth
                        | UIKit.UIViewAutoresizing.FlexibleHeight,
                };
                native.InsertSubview(blur, 0);
            }
        };
        return host;
#else
        // No cheap live blur on Android: a translucent fill keeps the under-scroll readable.
        return new BoxView { Color = Colors.White.WithAlpha(0.88f) };
#endif
    }

    private ScheduleView CreateDayView()
    {
        var view = new ScheduleView
        {
            ViewMode = 1,
            HeaderMode = ScheduleHeaderMode.Linked,
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
        view.SetBinding(
            ScheduleView.TopContentInsetProperty,
            new Binding(nameof(SharedScheduleState.HeaderInset), source: state));
        view.SetBinding(
            ScheduleView.PersonsProperty,
            new Binding(nameof(SharedScheduleState.Persons), source: state));

        // CarouselView recycles views across slots, so track which slot each view currently shows.
        view.BindingContextChanged += (_, _) => OnViewSlotChanged(view);
        return view;
    }

    private void OnViewSlotChanged(ScheduleView view)
    {
        foreach (var stale in viewsBySlot.Where(kv => ReferenceEquals(kv.Value, view)).ToList())
        {
            viewsBySlot.Remove(stale.Key);
        }

        if (view.BindingContext is DaySlot slot)
        {
            viewsBySlot[slot] = view;
            if (ReferenceEquals(carousel.CurrentItem, slot))
            {
                header.Schedule = view;
            }
        }
    }

    private void RelinkHeader(DaySlot? slot)
    {
        if (slot is not null && viewsBySlot.TryGetValue(slot, out var view))
        {
            header.Schedule = view;
        }
    }
}
