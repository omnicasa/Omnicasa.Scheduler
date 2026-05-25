using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>Top-level page showing the year calendar and drilling into a day on tap.</summary>
public partial class MainPage : ContentPage
{
    /// <summary>Gets the shared appointment source used across pages in the sample.</summary>
    public static InMemoryAppointmentSource Source { get; } = new InMemoryAppointmentSource();

    /// <summary>Initializes a new instance of the <see cref="MainPage"/> class.</summary>
    public MainPage()
    {
        InitializeComponent();
        Year.AppointmentSource = Source;
    }

    private async void OnDayTapped(object? sender, DayTappedEventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(DayPage)}?date={e.Date:yyyy-MM-dd}");
    }

    private async void OnOpenSchedule(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SchedulePage));
    }

    private async void OnOpenCarousel(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CarouselSchedulePage));
    }
}
