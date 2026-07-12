using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Demonstrates <see cref="ScheduleView.BlockoutsSource"/>: translucent "unavailable" bands
/// (before 09:00 and a 12:00–13:00 lunch each day) painted behind the shared sample appointments.
/// </summary>
public sealed class BlockoutSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="BlockoutSchedulePage"/> class.</summary>
    public BlockoutSchedulePage()
    {
        Title = "Blockout regions";

        var start = DateTime.Today;
        var end = start.AddDays(2);

        var offHours = Color.FromArgb("#8E8E93");
        var lunch = Color.FromArgb("#FF9500");
        var blockouts = new List<IScheduleBlockout>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            // Closed before the working day starts.
            blockouts.Add(new ScheduleBlockout
            {
                Start = day,
                End = day.AddHours(9),
                Title = "Closed",
                Color = offHours,
            });

            // Lunch break.
            blockouts.Add(new ScheduleBlockout
            {
                Start = day.AddHours(12),
                End = day.AddHours(13),
                Title = "Lunch",
                Color = lunch,
            });
        }

        var view = new ScheduleView
        {
            StartDay = start,
            EndDay = end,
            ViewMode = 3,
            ItemsSource = MainPage.Source.AllItems,
            BlockoutsSource = blockouts,
        };

        Content = view;

        // Bring the working day into view once laid out.
        Loaded += async (_, _) => await view.ScrollToTimeAsync(TimeSpan.FromHours(8), animated: false);
    }
}
