using System.Globalization;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Calendar host: drills down Year → Month → Day in a single page, animating each transition
/// with a scale + fade "zoom". Hardware back and the on-screen Back button step back up a level.
/// </summary>
public partial class MainPage : ContentPage
{
    private const uint ZoomDuration = 260;

    private Level level = Level.Year;

    private bool animating;

    /// <summary>Initializes a new instance of the <see cref="MainPage"/> class.</summary>
    public MainPage()
    {
        InitializeComponent();
        Year.AppointmentSource = Source;
        Month.AppointmentSource = Source;
        Day.AppointmentSource = Source;
    }

    private enum Level
    {
        Year,
        Month,
        Day,
    }

    /// <summary>Gets the shared appointment source used across pages in the sample.</summary>
    public static InMemoryAppointmentSource Source { get; } = new InMemoryAppointmentSource();

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
        if (level != Level.Year)
        {
            _ = GoBackAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnYearDayTapped(object? sender, DayTappedEventArgs e)
    {
        if (animating || level != Level.Year)
        {
            return;
        }

        Month.ScrollToMonth(e.Date.Year, e.Date.Month, animated: false);
        await DrillAsync(Year, Month);
        level = Level.Month;
        UpdateBackButton();
    }

    private async void OnMonthDayTapped(object? sender, DayTappedEventArgs e)
    {
        if (animating || level != Level.Month)
        {
            return;
        }

        Day.SelectedDate = e.Date.ToDateTime(TimeOnly.MinValue);
        DayHeader.Text = e.Date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
        await DrillAsync(Month, DayLayer);
        level = Level.Day;
        UpdateBackButton();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await GoBackAsync();

    private async Task GoBackAsync()
    {
        if (animating)
        {
            return;
        }

        switch (level)
        {
            case Level.Day:
                await UndrillAsync(DayLayer, Month);
                level = Level.Month;
                break;
            case Level.Month:
                await UndrillAsync(Month, Year);
                level = Level.Year;
                break;
            default:
                return;
        }

        UpdateBackButton();
    }

    // Drill in: the outgoing layer zooms out and fades; the incoming layer grows from small.
    private async Task DrillAsync(View outgoing, View incoming)
    {
        animating = true;

        incoming.Scale = 0.85;
        incoming.Opacity = 0;
        incoming.IsVisible = true;

        await Task.WhenAll(
            outgoing.ScaleTo(1.15, ZoomDuration, Easing.CubicOut),
            outgoing.FadeTo(0, ZoomDuration),
            incoming.ScaleTo(1, ZoomDuration, Easing.CubicOut),
            incoming.FadeTo(1, ZoomDuration));

        ResetHidden(outgoing);
        animating = false;
    }

    // Step back out: the current layer shrinks and fades; the one behind grows back from large.
    private async Task UndrillAsync(View outgoing, View incoming)
    {
        animating = true;

        incoming.Scale = 1.15;
        incoming.Opacity = 0;
        incoming.IsVisible = true;

        await Task.WhenAll(
            outgoing.ScaleTo(0.85, ZoomDuration, Easing.CubicIn),
            outgoing.FadeTo(0, ZoomDuration),
            incoming.ScaleTo(1, ZoomDuration, Easing.CubicOut),
            incoming.FadeTo(1, ZoomDuration));

        ResetHidden(outgoing);
        animating = false;
    }

    private void ResetHidden(View view)
    {
        view.IsVisible = false;
        view.Scale = 1;
        view.Opacity = 1;
    }

    private void UpdateBackButton() => BackButton.IsVisible = level != Level.Year;

    private async void OnOpenSchedule(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SchedulePage));
    }

    private async void OnOpenCarousel(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CarouselSchedulePage));
    }

    private async void OnOpenGlass(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(GlassSchedulePage));
    }

    private async void OnOpenAgenda(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AgendaPage));
    }

    private async void OnOpenWidget(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(WidgetPreviewPage));
    }

    private async void OnOpenRecurring(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecurringSchedulePage));
    }
}
