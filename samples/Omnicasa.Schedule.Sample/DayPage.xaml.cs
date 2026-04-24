using System.Globalization;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>Page showing the day-view agenda for a selected date.</summary>
[QueryProperty(nameof(DateString), "date")]
public partial class DayPage : ContentPage
{
    /// <summary>Gets or sets the date query-string parameter (yyyy-MM-dd).</summary>
    public string? DateString { get; set; }

    /// <summary>Initializes a new instance of the <see cref="DayPage"/> class.</summary>
    public DayPage()
    {
        InitializeComponent();
        Day.AppointmentSource = MainPage.Source;
        Day.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DayAgendaView.SelectedDate))
            {
                UpdateHeader();
            }
        };
        ModePicker.SelectedIndex = DaysPerPageToIndex(Day.DaysPerPage);
    }

    private static int DaysPerPageToIndex(int daysPerPage) => daysPerPage switch
    {
        3 => 1,
        5 => 2,
        7 => 3,
        _ => 0,
    };

    private static int IndexToDaysPerPage(int index) => index switch
    {
        1 => 3,
        2 => 5,
        3 => 7,
        _ => 1,
    };

    private void OnModeChanged(object? sender, EventArgs e)
    {
        Day.DaysPerPage = IndexToDaysPerPage(ModePicker.SelectedIndex);
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (DateOnly.TryParseExact(DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            Day.SelectedDate = d.ToDateTime(TimeOnly.MinValue);
        }

        UpdateHeader();
    }

    private void UpdateHeader()
    {
        HeaderLabel.Text = Day.SelectedDate.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
    }

    private async void OnAppointmentTapped(object? sender, AppointmentEventArgs e)
    {
        await DisplayAlert(e.Appointment.Title, $"{e.Appointment.Start:t} – {e.Appointment.End:t}", "OK");
    }

    private void OnAppointmentChanged(object? sender, AppointmentEventArgs e)
    {
        // Appointment properties are mutated in-place; nothing to persist for the in-memory demo source.
    }
}
