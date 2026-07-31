using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Detail page for one appointment, pushed with <see cref="ScheduleZoomNavigation"/> so it grows out
/// of the block the user tapped and shrinks back into it on the way out.
/// </summary>
public sealed class AppointmentDetailPage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="AppointmentDetailPage"/> class.</summary>
    /// <param name="item">The appointment to show.</param>
    public AppointmentDetailPage(IScheduleItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Title = item.Title ?? "(no title)";
        var accent = item.Color ?? Color.FromArgb("#007AFF");

        var card = new Border
        {
            Stroke = accent,
            StrokeThickness = 1,
            BackgroundColor = accent.WithAlpha(0.12f),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = item.Title ?? "(no title)", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = accent },
                    new Label { Text = FormatRange(item), FontSize = 15 },
                    new Label { Text = item.Notes ?? "No notes.", FontSize = 13, Opacity = 0.7 },
                },
            },
        };

        var close = new Button { Text = "Close", HorizontalOptions = LayoutOptions.Start };
        close.Clicked += async (_, _) => await ScheduleZoomNavigation.PopZoomAsync(this);

        Content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16,
            Children = { card, close },
        };
    }

    private static string FormatRange(IScheduleItem item)
        => item.IsAllDay
            ? $"{item.Start.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture)} · all day"
            : $"{item.Start.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)} – {item.End.ToString("HH:mm", CultureInfo.CurrentCulture)}";
}
