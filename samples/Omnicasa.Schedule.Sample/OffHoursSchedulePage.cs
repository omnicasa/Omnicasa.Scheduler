using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// A single-day <see cref="ScheduleView"/> with business-hours shading on: the hours before
/// <see cref="ScheduleView.WorkDayStart"/> and after <see cref="ScheduleView.WorkDayEnd"/> are
/// tinted so the working day (here 09:00–17:00) reads at a glance, like Outlook / Google Calendar.
/// </summary>
public sealed class OffHoursSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="OffHoursSchedulePage"/> class.</summary>
    public OffHoursSchedulePage()
    {
        Title = "Off-hours shading";

        var today = DateTime.Today;
        var view = new ScheduleView
        {
            ViewMode = 1,
            StartDay = today,
            EndDay = today,
            ItemsSource = MainPage.Source.AllItems,
            ShowOffHoursShading = true,
            WorkDayStart = TimeSpan.FromHours(9),
            WorkDayEnd = TimeSpan.FromHours(17),
        };

        Content = view;
    }
}
