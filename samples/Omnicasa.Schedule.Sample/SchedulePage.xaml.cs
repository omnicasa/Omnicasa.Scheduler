using System.Globalization;

namespace Omnicasa.Schedule.Sample;

/// <summary>Demos the read-only <see cref="ScheduleView"/>. UI state is in <see cref="SchedulePageViewModel"/>.</summary>
public partial class SchedulePage
{
    /// <summary>Initializes a new instance of the <see cref="SchedulePage"/> class.</summary>
    public SchedulePage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;

        // Long-press an appointment to show a native menu (iOS context menu / Android PopupMenu).
        Schedule.ItemActionsProvider = _ => new[]
        {
            // Icon names: iOS SF Symbols (Android would use drawable resource names).
            new ScheduleMenuAction("Edit", icon: "pencil"),
            new ScheduleMenuAction("Duplicate", icon: "doc.on.doc"),
            new ScheduleMenuAction("Delete", icon: "trash", isDestructive: true),
        };
        Schedule.ItemActionInvoked += OnItemActionInvoked;
    }

    private async void OnItemActionInvoked(object? sender, ScheduleItemActionEventArgs e)
    {
        await DisplayAlert(e.Item.Title ?? "(no title)", $"Action: {e.Action}", "OK");
    }

    private void OnHoldingDropped(object? sender, HoldingDroppedEventArgs e)
    {
        // Event-only: the control doesn't mutate the item, so apply the drop here to make it stick.
        // The item is an Appointment (INotifyPropertyChanged), so the block re-renders at the new spot.
        if (e.Item is Appointment appt)
        {
            appt.Start = e.Start;
            appt.End = e.End;
            appt.PersonId = e.PersonId;
        }
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Open near the current time (one hour of lead-in), clamped to midnight. Deferred so the
        // ScheduleView has laid out and its scrollable content has a measured height.
        var top = DateTime.Now.TimeOfDay - TimeSpan.FromHours(1);
        if (top < TimeSpan.Zero)
        {
            top = TimeSpan.Zero;
        }

        Dispatcher.Dispatch(() => _ = Schedule.ScrollToTimeAsync(top));
    }

    private async void OnTapped(object? sender, ScheduleTappedEventArgs e)
    {
        await DisplayAlert(
            "Tap",
            $"Empty space at {e.When.ToString("g", CultureInfo.CurrentCulture)}",
            "OK");
    }

    private void OnLongTapped(object? sender, ScheduleTappedEventArgs e)
    {
        // Long-tap on empty space toggles the draft: create one at that time, or dismiss the existing one.
        if (BindingContext is SchedulePageViewModel vm)
        {
            if (vm.TypingItem is not null)
            {
                vm.DismissDraft();
            }
            else
            {
                vm.CreateDraftAt(e.When);
            }
        }
    }

    private async void OnItemTapped(object? sender, ScheduleItemTappedEventArgs e)
    {
        var item = e.Item;
        await DisplayAlert(
            item.Title ?? "(no title)",
            $"Tap: {item.Start.ToString("g", CultureInfo.CurrentCulture)} – {item.End.ToString("t", CultureInfo.CurrentCulture)}",
            "OK");
    }

    private async void OnItemLongTapped(object? sender, ScheduleItemTappedEventArgs e)
    {
        var item = e.Item;
        await DisplayAlert(
            item.Title ?? "(no title)",
            $"Long tap: {item.Start.ToString("g", CultureInfo.CurrentCulture)} – {item.End.ToString("t", CultureInfo.CurrentCulture)}",
            "OK");
    }
}
