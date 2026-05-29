using System.Globalization;
#if ANDROID
using Android.Graphics;
using Omnicasa.Schedule.Widget;
#endif

namespace Omnicasa.Schedule.Sample;

/// <summary>
/// Visual harness for the native schedule widget. On Android it renders the widget's Bitmap
/// (via <c>ScheduleWidgetRenderer</c>, the same code the home-screen widget uses) and shows it in a
/// normal Image — so you can SEE the widget grid on a device/emulator without installing a widget.
/// On iOS it shows a note (use the Xcode SwiftUI preview for iOS — see widgets/ios).
/// </summary>
public sealed class WidgetPreviewPage : ContentPage
{
    private readonly Image image = new Image { Aspect = Aspect.AspectFit, BackgroundColor = Colors.White };

    /// <summary>Initializes a new instance of the <see cref="WidgetPreviewPage"/> class.</summary>
    public WidgetPreviewPage()
    {
        Title = "Widget preview";

        var note = new Label
        {
            Padding = new Thickness(16, 10),
            FontSize = 13,
            TextColor = Colors.Gray,
        };

#if ANDROID
        note.Text = "Android: rendered by ScheduleWidgetRenderer (the real widget code).";
#else
        note.Text = "iOS: open widgets/ios in Xcode and use the SwiftUI preview (#Preview).";
#endif

        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Children = { note, image },
        };
        Grid.SetRow(note, 0);
        Grid.SetRow(image, 1);

        Loaded += (_, _) => RenderPreview();
    }

    private void RenderPreview()
    {
#if ANDROID
        // Map the sample's appointments into the widget transport model.
        var widgetItems = MainPage.Source.AllItems
            .Where(a => !a.IsAllDay)
            .Select(a => new WidgetAppointment
            {
                AppointmentId = string.IsNullOrEmpty(a.Id) ? 0 : a.Id.GetHashCode(),
                Title = a.Title,
                StartTime = a.Start.ToString(WidgetAppointment.DateFormat, CultureInfo.InvariantCulture),
                EndTime = a.End.ToString(WidgetAppointment.DateFormat, CultureInfo.InvariantCulture),
                BackgroundColor = a.Color is null ? null : "#" + a.Color.ToArgbHex(),
            })
            .ToList();

        float density = Android.App.Application.Context.Resources?.DisplayMetrics?.Density ?? 2f;
        int widthPx = (int)(360 * density);
        int heightPx = (int)(440 * density);

        using var bitmap = ScheduleWidgetRenderer.Render(widgetItems, widthPx, heightPx, DateTime.Now, density);
        using var stream = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Png!, 100, stream);
        var bytes = stream.ToArray();
        image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
#endif
    }
}
