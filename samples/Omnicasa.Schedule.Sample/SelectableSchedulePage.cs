using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Tap-to-select demo: a <see cref="ScheduleView"/> whose <see cref="ScheduleView.SelectedItem"/>
/// drives a status <see cref="Label"/>. Tapping a block selects it (drawn with an emphasis ring)
/// and updates the label via <see cref="ScheduleView.SelectionChanged"/>.
/// </summary>
public sealed class SelectableSchedulePage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="SelectableSchedulePage"/> class.</summary>
    public SelectableSchedulePage()
    {
        Title = "Tap to select";

        var status = new Label
        {
            Text = "Tap an appointment to select it",
            Padding = new Thickness(12, 10),
            FontSize = 15,
        };

        var today = DateTime.Today;
        var schedule = new ScheduleView
        {
            ViewMode = 3,
            StartDay = today,
            EndDay = today.AddDays(2),
            ItemsSource = MainPage.Source.AllItems,
        };

        schedule.SelectionChanged += (_, e) =>
        {
            var when = $"{e.Item.Start:ddd HH:mm}–{e.Item.End:HH:mm}";
            status.Text = $"Selected: {e.Item.Title}  ({when})";
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children =
            {
                status,
                schedule,
            },
        };
        Grid.SetRow(status, 0);
        Grid.SetRow(schedule, 1);
    }
}
