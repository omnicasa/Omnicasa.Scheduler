using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// A single-day <see cref="ScheduleView"/> with the live current-time indicator on. The "now" line
/// ticks every minute while the page is visible; the button jumps the viewport to the current time.
/// </summary>
public sealed class LiveNowSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="LiveNowSchedulePage"/> class.</summary>
    public LiveNowSchedulePage()
    {
        Title = "Live now";

        var today = DateTime.Today;
        var schedule = new ScheduleView
        {
            ViewMode = 1,
            StartDay = today,
            EndDay = today,
            ShowCurrentTimeIndicator = true,
            ItemsSource = MainPage.Source.AllItems,
        };

        var scrollToNow = new Button { Text = "Scroll to now", Margin = new Thickness(12, 8) };
        scrollToNow.Clicked += async (_, _) => await schedule.ScrollToNowAsync(true);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children = { scrollToNow, schedule },
        };
        Grid.SetRow(scrollToNow, 0);
        Grid.SetRow(schedule, 1);
    }
}
