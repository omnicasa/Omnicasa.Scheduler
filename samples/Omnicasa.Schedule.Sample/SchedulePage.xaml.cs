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
