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

    /// <summary>Attaches a context-menu interaction for an <see cref="InfiniteScheduleView"/>. Returns the delegate to root it.</summary>
    public static object AttachIosInfinite(UIKit.UIView view, InfiniteScheduleView owner)
    {
        var menuDelegate = new InfiniteScheduleContextMenuDelegate(owner);
        view.AddInteraction(new UIKit.UIContextMenuInteraction(menuDelegate));
        view.UserInteractionEnabled = true;
        return menuDelegate;
    }
#endif

#if ANDROID
    /// <summary>Shows a native PopupMenu at the press point; invokes the callback with the chosen label.</summary>
    public static bool ShowAndroid(View canvas, PointF location, IReadOnlyList<ScheduleMenuAction> actions, Action<string> onInvoke)
    {
        if (canvas.Handler?.PlatformView is not Android.Views.View canvasView || canvasView.Context is null)
        {
            return false;
        }

        var context = canvasView.Context;

        // PopupMenu can only align to its anchor's bounds, and the anchor here is the whole canvas —
        // that's what parked the menu in the corner. ListPopupWindow takes explicit offsets, so it
        // can land at the finger like the iOS context menu, anchored to the canvas we already have.
        // Scale comes from the canvas itself (platform pixels per MAUI unit), not DisplayMetrics —
        // the two callers hand us points in their own canvas space, which isn't always display density.
        float scale = canvas.Width > 0
            ? (float)(canvasView.Width / canvas.Width)
            : context.Resources?.DisplayMetrics?.Density ?? 1f;
        int x = (int)(location.X * scale);
        int y = (int)(location.Y * scale);

        var popup = new Android.Widget.ListPopupWindow(context)
        {
            AnchorView = canvasView,
            Modal = true,

            // Vertical offset is measured from the anchor's bottom edge, so pull back by its height.
            HorizontalOffset = x,
            VerticalOffset = y - canvasView.Height,
        };

        float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var icons = new int[actions.Count];
        bool anyIcon = false;
        for (int i = 0; i < actions.Count; i++)
        {
            icons[i] = ResolveIcon(context, actions[i].Icon);
            anyIcon |= icons[i] != 0;
        }

        popup.SetAdapter(new MenuAdapter(context, actions, icons, anyIcon, density));

        // Width has to be measured up front: ListPopupWindow won't size itself to its content.
        popup.Width = MeasureWidth(context, actions, anyIcon, density);

        popup.ItemClick += (_, e) =>
        {
            if (e.Position >= 0 && e.Position < actions.Count)
            {
                onInvoke(actions[e.Position].Label);
            }

            popup.Dismiss();
        };

        popup.Show();
        return true;
    }

    // Widest label plus padding, clamped so a long title can't run off screen.
    private static int MeasureWidth(Android.Content.Context context, IReadOnlyList<ScheduleMenuAction> actions, bool anyIcon, float density)
    {
        var paint = new Android.Text.TextPaint { TextSize = 16 * density };
        float widest = 0;
        foreach (var action in actions)
        {
            widest = Math.Max(widest, paint.MeasureText(action.Label));
        }

        int padding = (int)((anyIcon ? 88 : 48) * density);
        int max = context.Resources?.DisplayMetrics?.WidthPixels ?? (int)(320 * density);
        return Math.Min((int)widest + padding, (int)(max * 0.8));
    }

    // App drawable first, then Android's stock set; SF Symbol names get aliased so one Icon
    // string can serve both platforms.
    private static int ResolveIcon(Android.Content.Context context, string? name)
    {
        if (string.IsNullOrEmpty(name) || context.Resources is null)
        {
            return 0;
        }

        int id = context.PackageName is null ? 0 : context.Resources.GetIdentifier(name, "drawable", context.PackageName);
        if (id == 0)
        {
            id = context.Resources.GetIdentifier(name, "drawable", "android");
        }

        if (id != 0)
        {
            return id;
        }

        string? alias = name switch
        {
            "pencil" or "square.and.pencil" => "ic_menu_edit",
            "trash" => "ic_menu_delete",
            "doc.on.doc" => "ic_menu_save",
            "arrow.up.and.down.and.arrow.left.and.right" => "ic_menu_directions",
            "info.circle" => "ic_menu_info_details",
            "calendar" => "ic_menu_my_calendar",
            "square.and.arrow.up" => "ic_menu_share",
            "magnifyingglass" => "ic_menu_search",
            _ => null,
        };

        return alias is null ? 0 : context.Resources.GetIdentifier(alias, "drawable", "android");
    }

    /// <summary>Rows of icon + label, since ListPopupWindow has no icon-aware stock layout.</summary>
    private sealed class MenuAdapter : Android.Widget.BaseAdapter
    {
        private static readonly Android.Graphics.Color DestructiveColor = Android.Graphics.Color.Argb(255, 211, 47, 47);

        private readonly Android.Content.Context context;
        private readonly IReadOnlyList<ScheduleMenuAction> actions;
        private readonly int[] icons;
        private readonly bool anyIcon;
        private readonly float density;

        public MenuAdapter(Android.Content.Context context, IReadOnlyList<ScheduleMenuAction> actions, int[] icons, bool anyIcon, float density)
        {
            this.context = context;
            this.actions = actions;
            this.icons = icons;
            this.anyIcon = anyIcon;
            this.density = density;
        }

        public override int Count => actions.Count;

        public override Java.Lang.Object? GetItem(int position) => null;

        public override long GetItemId(int position) => position;

        public override Android.Views.View GetView(int position, Android.Views.View? convertView, Android.Views.ViewGroup? parent)
        {
            var action = actions[position];
            var row = new Android.Widget.LinearLayout(context)
            {
                Orientation = Android.Widget.Orientation.Horizontal,
                LayoutParameters = new Android.Widget.AbsListView.LayoutParams(
                    Android.Views.ViewGroup.LayoutParams.MatchParent,
                    (int)(48 * density)),
            };
            row.SetGravity(Android.Views.GravityFlags.CenterVertical);
            row.SetPadding((int)(16 * density), 0, (int)(16 * density), 0);

            if (anyIcon)
            {
                var image = new Android.Widget.ImageView(context)
                {
                    LayoutParameters = new Android.Widget.LinearLayout.LayoutParams((int)(24 * density), (int)(24 * density))
                    {
                        RightMargin = (int)(16 * density),
                    },
                };

                if (icons[position] != 0)
                {
                    image.SetImageResource(icons[position]);
                    if (action.IsDestructive)
                    {
                        image.SetColorFilter(DestructiveColor);
                    }
                }

                row.AddView(image);
            }

            var text = new Android.Widget.TextView(context) { Text = action.Label, TextSize = 16 };
            text.SetSingleLine(true);
            text.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            if (action.IsDestructive)
            {
                text.SetTextColor(DestructiveColor);
            }

            row.AddView(text);
            return row;
        }
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

/// <summary>Builds a UIMenu for the appointment under the long-press on an <see cref="InfiniteScheduleView"/>.</summary>
internal sealed class InfiniteScheduleContextMenuDelegate : Foundation.NSObject, UIKit.IUIContextMenuInteractionDelegate
{
    private readonly InfiniteScheduleView owner;

    private RectF previewRect;

    /// <summary>Initializes a new instance of the <see cref="InfiniteScheduleContextMenuDelegate"/> class.</summary>
    /// <param name="owner">The schedule view used for hit-testing and raising the chosen action.</param>
    public InfiniteScheduleContextMenuDelegate(InfiniteScheduleView owner) => this.owner = owner;

    /// <summary>Returns the context-menu configuration for the appointment at the long-press location, or null.</summary>
    /// <param name="interaction">The interaction requesting a configuration.</param>
    /// <param name="location">Long-press location in the view's coordinates.</param>
    /// <returns>A configuration when an appointment with actions is under the press; otherwise null.</returns>
    public UIKit.UIContextMenuConfiguration? GetConfigurationForMenu(UIKit.UIContextMenuInteraction interaction, CoreGraphics.CGPoint location)
    {
        var hit = owner.HitTestMenuItem(new PointF((float)location.X, (float)location.Y));
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

    /// <summary>Lifts only the appointment block as the menu's highlight preview.</summary>
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

    /// <summary>Uses the same clipped preview when the menu dismisses.</summary>
    /// <param name="interaction">The interaction requesting the preview.</param>
    /// <param name="configuration">The configuration returned earlier.</param>
    /// <returns>A targeted preview clipped to the appointment's rounded rect, or null.</returns>
    [Foundation.Export("contextMenuInteraction:previewForDismissingMenuWithConfiguration:")]
    public UIKit.UITargetedPreview? GetPreviewForDismissingMenu(UIKit.UIContextMenuInteraction interaction, UIKit.UIContextMenuConfiguration configuration)
        => GetPreviewForHighlightingMenu(interaction, configuration);
}
#endif
