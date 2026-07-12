using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Demonstrates <see cref="RecurrenceExpander"/>: a single template appointment is expanded by a
/// Weekly Mon/Wed/Fri rule across the visible week and the resulting occurrences are fed straight
/// into a <see cref="ScheduleView"/>. Built in code so the expand call is easy to read.
/// </summary>
public sealed class RecurringSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="RecurringSchedulePage"/> class.</summary>
    public RecurringSchedulePage()
    {
        Title = "Recurring";

        // Show the current week (Sunday..Saturday).
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);

        // One template — a 09:30 standup — repeated every Mon/Wed/Fri.
        var template = new Appointment
        {
            Id = "standup",
            Title = "Daily standup",
            Start = weekStart.AddHours(9.5),
            End = weekStart.AddHours(10),
            Color = Colors.MediumSeaGreen,
        };

        var rule = new RecurrenceRule
        {
            Frequency = RecurrenceFrequency.Weekly,
            ByWeekday = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
        };

        var occurrences = RecurrenceExpander.Expand(
            template,
            rule,
            weekStart,
            weekEnd.AddDays(1));

        Content = new ScheduleView
        {
            ViewMode = 7,
            StartDay = weekStart,
            EndDay = weekEnd,
            ItemsSource = occurrences,
        };
    }
}
