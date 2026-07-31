using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Navigates with iOS 18's native zoom transition, so the tapped appointment block expands into the
/// destination page and collapses back into it — including UIKit's interactive drag-to-dismiss, all
/// driven by the render server rather than by animations on the UI thread.
///
/// <para>Feed it <c>ScheduleItemTappedEventArgs.ItemRect</c>:</para>
/// <code>
/// schedule.ItemTapped += async (_, e) =>
/// {
///     if (e.ItemRect is not RectF rect) return;
///     await ScheduleZoomNavigation.PushZoomAsync(
///         new AppointmentDetailPage(e.Item), schedule, rect, e.Item.Color);
/// };
/// </code>
///
/// <para>The zoom needs iOS 18 or newer. On iOS 17 and older — and on every other platform — the page
/// is pushed with the platform's normal transition instead, so the call is safe everywhere.</para>
/// </summary>
public static partial class ScheduleZoomNavigation
{
    /// <summary>
    /// Pushes <paramref name="destination"/> zooming out of <paramref name="itemRect"/>, a rect in
    /// <paramref name="sourceView"/>'s coordinates — i.e. <c>ScheduleItemTappedEventArgs.ItemRect</c>.
    /// </summary>
    /// <param name="destination">The page to push.</param>
    /// <param name="sourceView">The control the rect belongs to — the schedule that raised the tap.</param>
    /// <param name="itemRect">The tapped block's rect, in <paramref name="sourceView"/> coordinates.</param>
    /// <param name="accent">The block's colour, so the zoom starts from something that matches it.</param>
    /// <returns>A task that completes once the destination page is on screen.</returns>
    public static async Task PushZoomAsync(Page destination, View sourceView, RectF itemRect, Color? accent = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sourceView);

#if IOS
        if (await TryPushNativeZoomAsync(destination, sourceView, itemRect, accent))
        {
            return;
        }
#endif

        await sourceView.Navigation.PushAsync(destination, true);
    }

    /// <summary>
    /// Closes a page pushed by <see cref="PushZoomAsync"/>, collapsing it back into its block on
    /// iOS 18+. The back button and the drag-to-dismiss gesture do this for you; call it from your own
    /// close affordances.
    /// </summary>
    /// <param name="page">The page to close.</param>
    /// <returns>A task that completes once the page has been popped.</returns>
    public static Task PopZoomAsync(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Navigation.PopAsync(true);
    }
}
