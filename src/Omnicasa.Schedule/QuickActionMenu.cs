using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Platform glue that shows a native long-press menu for an appointment: a context menu (UIMenu)
/// on iOS and a PopupMenu on Android. No-op on other targets.
/// </summary>
internal static class QuickActionMenu
{
#if IOS
    /// <summary>Attaches a context-menu interaction to the body's platform view. Returns the delegate to root it.</summary>
    public static object AttachIos(UIKit.UIView view, ScheduleView owner)
    {
        var menuDelegate = new ScheduleContextMenuDelegate(owner);
        view.AddInteraction(new UIKit.UIContextMenuInteraction(menuDelegate));
        view.UserInteractionEnabled = true;
        return menuDelegate;
    }
#endif

#if ANDROID
    /// <summary>Shows a native PopupMenu anchored to the body view; invokes the callback with the chosen label.</summary>
    public static bool ShowAndroid(GraphicsView canvas, PointF location, IReadOnlyList<ScheduleMenuAction> actions, Action<string> onInvoke)
    {
        _ = location;
        if (canvas.Handler?.PlatformView is not Android.Views.View anchor || anchor.Context is null)
        {
            return false;
        }

        var context = anchor.Context;
        var popup = new Android.Widget.PopupMenu(context, anchor);
        bool anyIcon = false;
        for (int i = 0; i < actions.Count; i++)
        {
            var menuItem = popup.Menu?.Add(Android.Views.IMenu.None, i, i, new Java.Lang.String(actions[i].Label));
            var icon = actions[i].Icon;
            if (menuItem is not null && !string.IsNullOrEmpty(icon) && context.Resources is not null && context.PackageName is not null)
            {
                int resId = context.Resources.GetIdentifier(icon, "drawable", context.PackageName);
                if (resId != 0)
                {
                    menuItem.SetIcon(resId);
                    anyIcon = true;
                }
            }
        }

        // PopupMenu hides icons by default; force them on when any action supplied one (API 29+).
        if (anyIcon && OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            popup.SetForceShowIcon(true);
        }

        popup.MenuItemClick += (_, e) =>
        {
            int id = e.Item?.ItemId ?? -1;
            if (id >= 0 && id < actions.Count)
            {
                onInvoke(actions[id].Label);
            }
        };
        popup.Show();
        return true;
    }
#endif
}

#if IOS
/// <summary>Builds a UIMenu for the appointment under the long-press, hit-testing via the owning view.</summary>
internal sealed class ScheduleContextMenuDelegate : Foundation.NSObject, UIKit.IUIContextMenuInteractionDelegate
{
    private readonly ScheduleView owner;

    private RectF previewRect;

    /// <summary>Initializes a new instance of the <see cref="ScheduleContextMenuDelegate"/> class.</summary>
    /// <param name="owner">The schedule view used for hit-testing and raising the chosen action.</param>
    public ScheduleContextMenuDelegate(ScheduleView owner) => this.owner = owner;

    /// <summary>Returns the context-menu configuration for the appointment at the long-press location, or null.</summary>
    /// <param name="interaction">The interaction requesting a configuration.</param>
    /// <param name="location">Long-press location in the body view's coordinates.</param>
    /// <returns>A configuration when an appointment with actions is under the press; otherwise null.</returns>
    public UIKit.UIContextMenuConfiguration? GetConfigurationForMenu(UIKit.UIContextMenuInteraction interaction, CoreGraphics.CGPoint location)
    {
        var hit = owner.HitTestItemRect(new PointF((float)location.X, (float)location.Y));
        if (hit is null)
        {
            return null;
        }

        var (item, rect) = hit.Value;
        var actions = owner.GetItemActions(item);
        if (actions.Count == 0)
        {
            return null;
        }

        previewRect = rect;
        return UIKit.UIContextMenuConfiguration.Create(
            identifier: null,
            previewProvider: null,
            actionProvider: _ =>
            {
                var elements = new UIKit.UIMenuElement[actions.Count];
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    var image = string.IsNullOrEmpty(action.Icon) ? null : UIKit.UIImage.GetSystemImage(action.Icon);
                    var uiAction = UIKit.UIAction.Create(action.Label, image, null, _ => owner.RaiseItemAction(item, action.Label));
                    if (action.IsDestructive)
                    {
                        uiAction.Attributes = UIKit.UIMenuElementAttributes.Destructive;
                    }

                    elements[i] = uiAction;
                }

                return UIKit.UIMenu.Create(elements);
            });
    }

    /// <summary>Lifts only the appointment block (not the whole view) as the menu's highlight preview.</summary>
    /// <param name="interaction">The interaction requesting the preview.</param>
    /// <param name="configuration">The configuration returned earlier.</param>
    /// <returns>A targeted preview clipped to the appointment's rounded rect, or null.</returns>
    [Foundation.Export("contextMenuInteraction:previewForHighlightingMenuWithConfiguration:")]
    public UIKit.UITargetedPreview? GetPreviewForHighlightingMenu(UIKit.UIContextMenuInteraction interaction, UIKit.UIContextMenuConfiguration configuration)
    {
        if (interaction.View is not UIKit.UIView view)
        {
            return null;
        }

        var rect = new CoreGraphics.CGRect(previewRect.Left, previewRect.Top, previewRect.Width, previewRect.Height);

        // Lift a snapshot of just the block — not the canvas itself — so the other appointments
        // stay visible (using the canvas as the preview view hides the whole thing during the lift).
        var snapshot = view.ResizableSnapshotView(rect, false, UIKit.UIEdgeInsets.Zero);
        if (snapshot is null)
        {
            return null;
        }

        var bounds = new CoreGraphics.CGRect(0, 0, rect.Width, rect.Height);
        snapshot.Frame = bounds;
        var parameters = new UIKit.UIPreviewParameters
        {
            VisiblePath = UIKit.UIBezierPath.FromRoundedRect(bounds, 8),
            BackgroundColor = UIKit.UIColor.Clear,
        };

        var center = new CoreGraphics.CGPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var target = new UIKit.UIPreviewTarget(view, center);
        return new UIKit.UITargetedPreview(snapshot, parameters, target);
    }

    /// <summary>Uses the same clipped preview when the menu dismisses, so the block animates back in place.</summary>
    /// <param name="interaction">The interaction requesting the preview.</param>
    /// <param name="configuration">The configuration returned earlier.</param>
    /// <returns>A targeted preview clipped to the appointment's rounded rect, or null.</returns>
    [Foundation.Export("contextMenuInteraction:previewForDismissingMenuWithConfiguration:")]
    public UIKit.UITargetedPreview? GetPreviewForDismissingMenu(UIKit.UIContextMenuInteraction interaction, UIKit.UIContextMenuConfiguration configuration)
        => GetPreviewForHighlightingMenu(interaction, configuration);
}
#endif
