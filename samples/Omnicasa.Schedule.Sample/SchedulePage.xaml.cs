using System.Globalization;

namespace Omnicasa.Schedule.Sample;

/// <summary>Demos the read-only <see cref="ScheduleView"/>. UI state is in <see cref="SchedulePageViewModel"/>.</summary>
public partial class SchedulePage
{
    /// <summary>Initializes a new instance of the <see cref="SchedulePage"/> class.</summary>
    public SchedulePage()
    {
        InitializeComponent();
    }

    private async void OnTapped(object? sender, ScheduleTappedEventArgs e)
    {
        await DisplayAlert(
            "Tap",
            $"Empty space at {e.When.ToString("g", CultureInfo.CurrentCulture)}",
            "OK");
    }

    private async void OnLongTapped(object? sender, ScheduleTappedEventArgs e)
    {
        await DisplayAlert(
            "Long tap",
            $"Empty space at {e.When.ToString("g", CultureInfo.CurrentCulture)}",
            "OK");
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
