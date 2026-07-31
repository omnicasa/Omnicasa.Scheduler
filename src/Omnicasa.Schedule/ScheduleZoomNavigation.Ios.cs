#if IOS
using CoreGraphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace Omnicasa.Schedule;

/// <summary>iOS 18 native zoom transition, used in place of the cross-platform stand-in when available.</summary>
public static partial class ScheduleZoomNavigation
{
    /// <summary>
    /// Pushes with UIKit's own zoom transition, which morphs a source view into the pushed page and
    /// gives interactive drag-to-dismiss for free — all driven by the render server, so it stays
    /// smooth regardless of what the UI thread is doing.
    ///
    /// <para>The transition needs a source <c>UIView</c>, and blocks are canvas pixels, so a
    /// non-interactive proxy tinted like the block is parked over it for as long as the pushed page
    /// lives. Returns false on iOS 17 and older, or when the views aren't realized yet, so the caller
    /// can fall back.</para>
    /// </summary>
    private static async Task<bool> TryPushNativeZoomAsync(Page destination, View sourceView, RectF itemRect, Color? accent)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(18))
        {
            return false;
        }

        if (sourceView.Handler is not { MauiContext: { } mauiContext, PlatformView: UIView platformSource })
        {
            return false;
        }

        // Realize the page's view controller up front: the transition has to be set before the push.
        if (destination.ToHandler(mauiContext) is not IPlatformViewHandler { ViewController: { } viewController })
        {
            return false;
        }

        var proxy = new UIView(new CGRect(itemRect.X, itemRect.Y, itemRect.Width, itemRect.Height))
        {
            UserInteractionEnabled = false,
            BackgroundColor = (accent ?? Colors.Transparent).WithAlpha(0.18f).ToPlatform(),
        };
        proxy.Layer.CornerRadius = 6;
        platformSource.AddSubview(proxy);

        // Dismissing zooms back into the same proxy, so it has to outlive the push.
        void OnUnloaded(object? sender, EventArgs e)
        {
            destination.Unloaded -= OnUnloaded;
            proxy.RemoveFromSuperview();
            proxy.Dispose();
        }

        destination.Unloaded += OnUnloaded;

        viewController.PreferredTransition = UIViewControllerTransition.Zoom(
            new UIZoomTransitionOptions(),
            _ => proxy);

        await sourceView.Navigation.PushAsync(destination, true);
        return true;
    }
}
#endif
