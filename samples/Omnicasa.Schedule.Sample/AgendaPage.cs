using System.Globalization;
using Microsoft.Maui.Controls;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>Demos the grouped, infinite <see cref="AgendaListView"/> against the shared appointment source.</summary>
public sealed class AgendaPage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="AgendaPage"/> class.</summary>
    public AgendaPage()
    {
        Title = "Agenda";

        var agenda = new AgendaListView
        {
            ItemsSource = MainPage.Source.AllItems,
            AnchorDate = DateTime.Today,
        };
        agenda.ItemTapped += OnItemTapped;

        Content = agenda;
    }

    private async void OnItemTapped(object? sender, ScheduleItemTappedEventArgs e)
    {
        var item = e.Item;
        await DisplayAlert(
            item.Title ?? "(no title)",
            $"{item.Start.ToString("g", CultureInfo.CurrentCulture)} – {item.End.ToString("t", CultureInfo.CurrentCulture)}",
            "OK");
    }
}
