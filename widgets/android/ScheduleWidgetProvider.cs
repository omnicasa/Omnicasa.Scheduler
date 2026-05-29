using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace Omnicasa.Schedule.Widget;

/// <summary>
/// EXAMPLE home-screen widget that renders the schedule time-grid. RemoteViews can't host a custom
/// view, so we render the grid to a bitmap (<see cref="ScheduleWidgetRenderer"/>) and push it into an
/// ImageView. Reads the same shared JSON the app already writes for the agenda widget.
///
/// To use: register in AndroidManifest.xml as a <c>receiver</c> with an
/// <c>android.appwidget.action.APPWIDGET_UPDATE</c> intent-filter and an
/// <c>android.appwidget.provider</c> meta-data pointing at <c>schedule_widget_info.xml</c>.
/// </summary>
[BroadcastReceiver(Label = "Schedule", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/schedule_widget_info")]
public sealed class ScheduleWidgetProvider : AppWidgetProvider
{
    /// <summary>App-relative path of the shared appointments JSON (matches the agenda widget).</summary>
    public const string SharedRelativePath = "Widgets/widget_appointments.json";

    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        foreach (int id in appWidgetIds)
        {
            Update(context, appWidgetManager, id);
        }
    }

    private static void Update(Context context, AppWidgetManager manager, int widgetId)
    {
        var appointments = LoadShared(context);

        // Pixel size for this widget instance, with sensible fallbacks.
        var options = manager.GetAppWidgetOptions(widgetId);
        float density = context.Resources?.DisplayMetrics?.Density ?? 2f;
        int minWidthDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMinWidth, 0) ?? 0;
        int minHeightDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMinHeight, 0) ?? 0;
        int widthPx = (int)((minWidthDp > 0 ? minWidthDp : 320) * density);
        int heightPx = (int)((minHeightDp > 0 ? minHeightDp : 320) * density);

        var bitmap = ScheduleWidgetRenderer.Render(appointments, widthPx, heightPx, DateTime.Now, density);

        var views = new RemoteViews(context.PackageName, Resource.Layout.schedule_widget_layout);
        views.SetImageViewBitmap(Resource.Id.schedule_image, bitmap);

        // Tap anywhere opens the app (point this at your launch / deep-link intent).
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launch is not null)
        {
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            var pending = PendingIntent.GetActivity(context, 0, launch, flags);
            views.SetOnClickPendingIntent(Resource.Id.schedule_image, pending);
        }

        manager.UpdateAppWidget(widgetId, views);
    }

    public override void OnAppWidgetOptionsChanged(Context context, AppWidgetManager manager, int widgetId, Android.OS.Bundle newOptions)
        => Update(context, manager, widgetId);

    private static IReadOnlyList<WidgetAppointment> LoadShared(Context context)
    {
        try
        {
            // Adjust to wherever the app writes the shared file (the agenda widget uses the same path).
            var baseDir = context.GetExternalFilesDir(null)?.AbsolutePath
                          ?? context.FilesDir?.AbsolutePath;
            if (baseDir is null)
            {
                return Array.Empty<WidgetAppointment>();
            }

            var path = System.IO.Path.Combine(baseDir, SharedRelativePath);
            if (!System.IO.File.Exists(path))
            {
                return Array.Empty<WidgetAppointment>();
            }

            return WidgetAppointmentShare.Read(System.IO.File.ReadAllText(path));
        }
        catch (System.IO.IOException)
        {
            return Array.Empty<WidgetAppointment>();
        }
    }
}
