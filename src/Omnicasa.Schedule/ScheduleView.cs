using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>Event payload carrying a tapped <see cref="IScheduleItem"/>.</summary>
public sealed class ScheduleItemTappedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleItemTappedEventArgs"/> class.</summary>
    /// <param name="item">The tapped item.</param>
    public ScheduleItemTappedEventArgs(IScheduleItem item)
    {
        Item = item;
    }

    /// <summary>Gets the tapped item.</summary>
    public IScheduleItem Item { get; }
}

/// <summary>Event payload carrying the date/time of an empty-space tap.</summary>
public sealed class ScheduleTappedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleTappedEventArgs"/> class.</summary>
    /// <param name="when">Date and time at the tap location.</param>
    /// <param name="personId">Person whose sub-column was tapped, or null in single-person mode.</param>
    public ScheduleTappedEventArgs(DateTime when, string? personId = null)
    {
        When = when;
        PersonId = personId;
    }

    /// <summary>Gets the date and time at the tap location (day of the column + time-of-day at the tap Y).</summary>
    public DateTime When { get; }

    /// <summary>Gets the person whose sub-column was tapped (per-person mode), or null.</summary>
    public string? PersonId { get; }
}

/// <summary>Event payload for an action chosen from an appointment's long-press menu.</summary>
public sealed class ScheduleItemActionEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleItemActionEventArgs"/> class.</summary>
    /// <param name="item">The appointment the menu was shown for.</param>
    /// <param name="action">The chosen action label.</param>
    public ScheduleItemActionEventArgs(IScheduleItem item, string action)
    {
        Item = item;
        Action = action;
    }

    /// <summary>Gets the appointment the menu was shown for.</summary>
    public IScheduleItem Item { get; }

    /// <summary>Gets the chosen action label (one of the strings returned by the actions provider).</summary>
    public string Action { get; }
}

/// <summary>Event payload for a dropped <c>HoldingSchedule</c> block.</summary>
public sealed class HoldingDroppedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="HoldingDroppedEventArgs"/> class.</summary>
    /// <param name="item">The held item that was dropped.</param>
    /// <param name="start">The snapped start time at the drop location.</param>
    /// <param name="end">The snapped end time at the drop location (reflects any resize).</param>
    /// <param name="personId">The person id of the column dropped on, or null.</param>
    public HoldingDroppedEventArgs(IScheduleItem item, DateTime start, DateTime end, string? personId)
    {
        Item = item;
        Start = start;
        End = end;
        PersonId = personId;
    }

    /// <summary>Gets the held item that was dropped (unchanged by the control).</summary>
    public IScheduleItem Item { get; }

    /// <summary>Gets the snapped start time at the drop location.</summary>
    public DateTime Start { get; }

    /// <summary>Gets the snapped end time at the drop location (reflects any resize).</summary>
    public DateTime End { get; }

    /// <summary>Gets the person id of the column the block was dropped on, or null.</summary>
    public string? PersonId { get; }
}

/// <summary>
/// Minimal read-only schedule control. Renders a fixed [<see cref="StartDay"/>, <see cref="EndDay"/>]
/// viewport (capped to <see cref="ViewMode"/> columns), optionally splitting each day into
/// per-<see cref="Person"/> sub-columns. The day header bar stays pinned at the top while the
/// hour grid scrolls underneath. Supports tap / long-tap (with date/time payload), and pinch-to-zoom.
/// </summary>
public class ScheduleView : ContentView
{
    private const int LongPressMilliseconds = 400;

    private const float LongPressMoveThreshold = 12f;

    private const float AllDayLaneHeight = 22f;

    /// <summary>Bindable property for <see cref="StartDay"/>.</summary>
    public static readonly BindableProperty StartDayProperty =
        BindableProperty.Create(
            nameof(StartDay),
            typeof(DateTime),
            typeof(ScheduleView),
            DateTime.Today,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="EndDay"/>.</summary>
    public static readonly BindableProperty EndDayProperty =
        BindableProperty.Create(
            nameof(EndDay),
            typeof(DateTime),
            typeof(ScheduleView),
            DateTime.Today.AddDays(6),
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="ViewMode"/>.</summary>
    public static readonly BindableProperty ViewModeProperty =
        BindableProperty.Create(
            nameof(ViewMode),
            typeof(int),
            typeof(ScheduleView),
            7,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild(),
            coerceValue: (_, v) => Math.Clamp((int)v, 1, 7));

    /// <summary>Bindable property for <see cref="HourHeight"/>.</summary>
    public static readonly BindableProperty HourHeightProperty =
        BindableProperty.Create(
            nameof(HourHeight),
            typeof(double),
            typeof(ScheduleView),
            60.0,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild(),
            coerceValue: (_, v) => Math.Clamp((double)v, 24.0, 200.0));

    /// <summary>Bindable property for <see cref="VerticalOffset"/>.</summary>
    public static readonly BindableProperty VerticalOffsetProperty =
        BindableProperty.Create(
            nameof(VerticalOffset),
            typeof(double),
            typeof(ScheduleView),
            0.0,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, n) => ((ScheduleView)b).ApplyVerticalOffset((double)n));

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleViewTheme),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(ScheduleViewRenderer),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleView)b).OnRendererChanged());

    /// <summary>Bindable property for <see cref="Persons"/>.</summary>
    public static readonly BindableProperty PersonsProperty =
        BindableProperty.Create(
            nameof(Persons),
            typeof(IList<IPerson>),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, o, n) => ((ScheduleView)b).OnItemsSourceChanged(o as IEnumerable, n as IEnumerable));

    /// <summary>Bindable property for <see cref="TypingItem"/>.</summary>
    public static readonly BindableProperty TypingItemProperty =
        BindableProperty.Create(
            nameof(TypingItem),
            typeof(ITypingScheduleItem),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, o, n) => ((ScheduleView)b).OnTypingItemChanged(o as ITypingScheduleItem, n as ITypingScheduleItem));

    /// <summary>Bindable property for <see cref="TopContentInset"/>.</summary>
    public static readonly BindableProperty TopContentInsetProperty =
        BindableProperty.Create(
            nameof(TopContentInset),
            typeof(double),
            typeof(ScheduleView),
            0.0,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild(),
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    /// <summary>Bindable property for <see cref="BottomContentInset"/>.</summary>
    public static readonly BindableProperty BottomContentInsetProperty =
        BindableProperty.Create(
            nameof(BottomContentInset),
            typeof(double),
            typeof(ScheduleView),
            0.0,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild(),
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    /// <summary>Bindable property for <see cref="HeaderMode"/>.</summary>
    public static readonly BindableProperty HeaderModeProperty =
        BindableProperty.Create(
            nameof(HeaderMode),
            typeof(ScheduleHeaderMode),
            typeof(ScheduleView),
            ScheduleHeaderMode.Inhouse,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="HoldingSchedule"/>.</summary>
    public static readonly BindableProperty HoldingScheduleProperty =
        BindableProperty.Create(
            nameof(HoldingSchedule),
            typeof(IScheduleItem),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, o, n) => ((ScheduleView)b).OnHoldingScheduleChanged(o as IScheduleItem, n as IScheduleItem));

    private readonly ScheduleViewTheme fallbackTheme = new ScheduleViewTheme();

    private readonly ScheduleRenderContext context = new ScheduleRenderContext();

    private readonly ScheduleBodyDrawable bodyDrawable;

    private readonly ScheduleHeaderDrawable headerDrawable;

    private readonly ScheduleAllDayDrawable allDayDrawable;

    private readonly GraphicsView headerCanvas;

    // Hidden canvases must collapse via their row heights: a hidden child with HeightRequest=0
    // still leaves the Auto row a few points tall, pushing the body scroll down (visible as a
    // bare strip behind a transparent linked header).
    private readonly RowDefinition headerRow = new RowDefinition { Height = GridLength.Auto };

    private readonly RowDefinition allDayRow = new RowDefinition { Height = GridLength.Auto };

    private readonly GraphicsView allDayCanvas;

    private readonly GraphicsView bodyCanvas;

    private readonly ScrollView bodyScroll;

    private readonly IDispatcherTimer longPressTimer;

    // Debounces VerticalOffset publication: pushing the offset per scrolled frame fanned out
    // through the binding to every live carousel page (each re-scrolling natively) at frame
    // rate. Synced siblings only need the offset once scrolling settles.
    private readonly IDispatcherTimer offsetPublishTimer;

    private PointF pointerDownPoint;

    private bool pointerDown;

    private bool longTapFired;

    private bool moveCanceledTap;

    // Breaks the feedback loop between user scrolling (updates VerticalOffset) and a
    // bound VerticalOffset (scrolls the view) so synced carousel pages don't fight.
    private bool suppressOffsetSync;

    // Coalesces the rebuilds triggered by property changes: realizing a carousel page applies
    // ~15 bindings back-to-back, which used to run a full rebuild (and full-height redraw) per
    // property. One dispatched pass per UI cycle instead.
    private bool rebuildQueued;

    private int rebuildCount;

    private double? pendingOffset;

    private double pinchBase = 60;

    private double pinchScale = 1.0;

    private double pinchAnchorHours = -1;

    private double pinchAnchorViewportY;

    private TypingDragMode typingDragMode = TypingDragMode.None;

    private DateTime typingOriginStart;

    private DateTime typingOriginEnd;

    private PointF typingOriginPoint;

    private TypingDragMode holdingDragMode = TypingDragMode.None;

    private PointF holdingOriginPoint;

    private DateTime holdingOriginStart;

    private DateTime holdingOriginEnd;

    private DateTime holdingDragStart;

    private DateTime holdingDragEnd;

    private int holdingDragColumn = -1;

#if IOS
    private bool iosMenuAttached;

    private object? menuPlatformState;
#endif

    /// <summary>Initializes a new instance of the <see cref="ScheduleView"/> class.</summary>
    public ScheduleView()
    {
        bodyDrawable = new ScheduleBodyDrawable { Context = context };

        headerDrawable = new ScheduleHeaderDrawable { Context = context };

        headerCanvas = new GraphicsView
        {
            Drawable = headerDrawable,
            BackgroundColor = Colors.Transparent,
            InputTransparent = true,
        };

        allDayDrawable = new ScheduleAllDayDrawable { Context = context };

        allDayCanvas = new GraphicsView
        {
            Drawable = allDayDrawable,
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
        };
        var allDayTap = new TapGestureRecognizer();
        allDayTap.Tapped += OnAllDayTapped;
        allDayCanvas.GestureRecognizers.Add(allDayTap);

        bodyCanvas = new GraphicsView
        {
            Drawable = bodyDrawable,
            BackgroundColor = Colors.Transparent,
        };

#if IOS
        // The native iOS context menu is system-driven, so attach a UIContextMenuInteraction to the
        // body's platform view once its handler exists.
        bodyCanvas.HandlerChanged += OnBodyCanvasHandlerChanged;
#endif

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += OnPointerPressed;
        pointer.PointerMoved += OnPointerMoved;
        pointer.PointerReleased += OnPointerReleased;
        pointer.PointerExited += OnPointerCanceled;
        bodyCanvas.GestureRecognizers.Add(pointer);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        bodyCanvas.GestureRecognizers.Add(pinch);

        bodyScroll = new ScrollView
        {
            Content = bodyCanvas,
            Orientation = ScrollOrientation.Vertical,
        };
        bodyScroll.Scrolled += (_, e) =>
        {
            CancelPendingLongPress();
            OnBodyScrolled(e.ScrollY);
        };

#if IOS
        // UIScrollView delays touch delivery to its content while deciding whether the gesture is a
        // scroll, so a quick press-and-drag on a holding/typing block started a scroll instead of the
        // drag (the press only reached the canvas after a stationary hold). Deliver touches
        // immediately — the drag paths claim the gesture on press and disable scrolling themselves.
        bodyScroll.HandlerChanged += (_, _) =>
        {
            if (bodyScroll.Handler?.PlatformView is UIKit.UIScrollView nativeScroll)
            {
                nativeScroll.DelaysContentTouches = false;
            }
        };
#endif

        longPressTimer = Dispatcher.CreateTimer();
        longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressMilliseconds);
        longPressTimer.IsRepeating = false;
        longPressTimer.Tick += OnLongPressTick;

        offsetPublishTimer = Dispatcher.CreateTimer();
        offsetPublishTimer.Interval = TimeSpan.FromMilliseconds(150);
        offsetPublishTimer.IsRepeating = false;
        offsetPublishTimer.Tick += (_, _) => PublishPendingOffset();

        var root = new Grid
        {
            RowDefinitions =
            {
                headerRow,
                allDayRow,
                new RowDefinition { Height = GridLength.Star },
            },
        };
        root.Children.Add(headerCanvas);
        Grid.SetRow(headerCanvas, 0);
        root.Children.Add(allDayCanvas);
        Grid.SetRow(allDayCanvas, 1);
        root.Children.Add(bodyScroll);
        Grid.SetRow(bodyScroll, 2);
        Content = root;

        Loaded += (_, _) =>
        {
            Rebuild();

            // A freshly realized carousel page must jump to the shared offset once its
            // content has a size; defer so the ScrollView has measured first.
            if (VerticalOffset > 0)
            {
                Dispatcher.Dispatch(() => ApplyVerticalOffset(VerticalOffset));
            }
        };
    }

    /// <summary>Fired when the user taps an empty area of the body. Payload is the day + time of the tap.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? Tapped;

    /// <summary>Fired when the user taps an appointment block.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemTapped;

    /// <summary>Fired when the user long-presses an empty area. Payload is the day + time at the press.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? LongTapped;

    /// <summary>Fired when the user long-presses an appointment block.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemLongTapped;

    /// <summary>Fired when the user picks an action from an appointment's long-press menu.</summary>
    public event EventHandler<ScheduleItemActionEventArgs>? ItemActionInvoked;

    /// <summary>Fired when the held <see cref="HoldingSchedule"/> block is released. The item is not mutated.</summary>
    public event EventHandler<HoldingDroppedEventArgs>? HoldingDropped;

    /// <summary>Raised after the render context is rebuilt, so a linked <see cref="ScheduleHeaderView"/> can refresh.</summary>
    internal event EventHandler? Rebuilt;

    /// <summary>
    /// Optional provider of long-press menu actions for an appointment. Return the actions to offer
    /// (label + optional icon); an empty or absent list means no menu (long-press then falls back to
    /// <see cref="ItemLongTapped"/>). On iOS the actions build a native context menu (UIMenu) with
    /// SF Symbol icons; on Android a native <c>PopupMenu</c>. Selecting one raises
    /// <see cref="ItemActionInvoked"/> with the chosen <see cref="ScheduleMenuAction.Label"/>.
    /// </summary>
    public Func<IScheduleItem, IReadOnlyList<ScheduleMenuAction>>? ItemActionsProvider { get; set; }

    /// <summary>First day rendered (inclusive).</summary>
    public DateTime StartDay
    {
        get => (DateTime)GetValue(StartDayProperty);
        set => SetValue(StartDayProperty, value);
    }

    /// <summary>Last day rendered (inclusive).</summary>
    public DateTime EndDay
    {
        get => (DateTime)GetValue(EndDayProperty);
        set => SetValue(EndDayProperty, value);
    }

    /// <summary>Maximum columns to display (1..7). Effective columns are clamped to the [<see cref="StartDay"/>, <see cref="EndDay"/>] range.</summary>
    public int ViewMode
    {
        get => (int)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>Vertical pixels per hour. Adjustable via pinch (clamped 24..200).</summary>
    public double HourHeight
    {
        get => (double)GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    /// <summary>
    /// Current vertical scroll position of the body, in logical pixels (0 = midnight at the top).
    /// Two-way: scrolling updates it, and setting it scrolls the view. Bind every page's
    /// <see cref="VerticalOffset"/> to one shared value so a <c>CarouselView</c> of schedules keeps
    /// the same boundary time when you swipe between pages (they share <see cref="HourHeight"/>).
    /// </summary>
    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>Theme bundle (colors + font sizes). Defaults to a built-in light theme.</summary>
    public ScheduleViewTheme Theme
    {
        get => (ScheduleViewTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Custom painter for the body and header. Defaults to <see cref="ScheduleViewRenderer.Default"/>.
    /// Subclass <see cref="ScheduleViewRenderer"/> and override <c>DrawAppointment</c> (switching on the
    /// item's concrete type) to render different appointment types differently.
    /// </summary>
    public ScheduleViewRenderer Renderer
    {
        get => (ScheduleViewRenderer)GetValue(RendererProperty) ?? ScheduleViewRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Optional persons. When non-empty, each day splits into one sub-column per person.</summary>
    public IList<IPerson>? Persons
    {
        get => (IList<IPerson>?)GetValue(PersonsProperty);
        set => SetValue(PersonsProperty, value);
    }

    /// <summary>Items rendered.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Optional draft item shown as a highlighted shadowed overlay. Touch on its body moves it,
    /// touch on its top/bottom edge resizes it. <see cref="ITypingScheduleItem.Start"/>,
    /// <see cref="ITypingScheduleItem.End"/>, and <see cref="ITypingScheduleItem.PersonId"/> are
    /// mutated in place during the gesture.
    /// </summary>
    public ITypingScheduleItem? TypingItem
    {
        get => (ITypingScheduleItem?)GetValue(TypingItemProperty);
        set => SetValue(TypingItemProperty, value);
    }

    /// <summary>
    /// Optional "held" item drawn as a floating block. Touch it to drag — free vertically (any time),
    /// snapped horizontally to the nearest column. On release, <see cref="HoldingDropped"/> fires with
    /// the snapped time/person; the item itself is not modified (the app decides whether to apply it).
    /// </summary>
    public IScheduleItem? HoldingSchedule
    {
        get => (IScheduleItem?)GetValue(HoldingScheduleProperty);
        set => SetValue(HoldingScheduleProperty, value);
    }

    /// <summary>
    /// Where the header (and all-day panel) is rendered. <see cref="ScheduleHeaderMode.Inhouse"/>
    /// draws them pinned inside this control (default). <see cref="ScheduleHeaderMode.Linked"/>
    /// suppresses both so an external <see cref="ScheduleHeaderView"/> (with its
    /// <see cref="ScheduleHeaderView.Schedule"/> set to this view) renders them — e.g. as a
    /// translucent glass bar the body scrolls under. <see cref="ScheduleHeaderMode.None"/> hides
    /// the header but keeps the all-day panel.
    /// </summary>
    public ScheduleHeaderMode HeaderMode
    {
        get => (ScheduleHeaderMode)GetValue(HeaderModeProperty);
        set => SetValue(HeaderModeProperty, value);
    }

    /// <summary>
    /// Blank space above midnight inside the scrollable body, in logical pixels. Use with
    /// <see cref="ScheduleHeaderMode.Linked"/> and an overlaid <see cref="ScheduleHeaderView"/>:
    /// size it to the header's height so hour 0 starts fully visible below the glass bar while
    /// scrolled content still passes underneath it.
    /// </summary>
    public double TopContentInset
    {
        get => (double)GetValue(TopContentInsetProperty);
        set => SetValue(TopContentInsetProperty, value);
    }

    /// <summary>
    /// Blank space below the 24:00 line inside the scrollable body, in logical pixels. Give it a
    /// few points (e.g. the hour-label font size) so the last hour label stays fully visible
    /// instead of being clipped by the bottom edge; pair with <see cref="TopContentInset"/> for
    /// the 00:00 label. Paint into the strip via <see cref="ScheduleViewRenderer.DrawBodyFooter"/>.
    /// </summary>
    public double BottomContentInset
    {
        get => (double)GetValue(BottomContentInsetProperty);
        set => SetValue(BottomContentInsetProperty, value);
    }

    /// <summary>Render state shared with a linked <see cref="ScheduleHeaderView"/>.</summary>
    internal ScheduleRenderContext RenderContext => context;

    /// <summary>
    /// Scrolls the body so <paramref name="timeOfDay"/> sits at the top edge — the library does the
    /// time→pixel conversion for you (no need to multiply by <see cref="HourHeight"/>). Also updates
    /// <see cref="VerticalOffset"/>, so views bound to the same offset follow along.
    /// Example: <c>ScrollToTimeAsync(TimeSpan.FromHours(10))</c> or <c>ScrollToTimeAsync(new TimeSpan(16, 0, 0))</c>.
    /// </summary>
    /// <param name="timeOfDay">Target time of day to bring to the top of the viewport.</param>
    /// <param name="animated">Whether to animate the scroll.</param>
    /// <returns>A task that completes when the scroll finishes.</returns>
    public Task ScrollToTimeAsync(TimeSpan timeOfDay, bool animated = false)
    {
        // Property changes queue their rebuild; run it now so the time→pixel scale is current.
        FlushPendingRebuild();

        // Minus the inset so the time lands just below an overlaid header, not under it.
        double offset = context.Scale.YForTime(timeOfDay) - context.Scale.TopPadding;

        // Publish the offset for synced siblings, but suppress our own (non-animated) re-scroll —
        // we scroll explicitly below so the `animated` flag is honored.
        suppressOffsetSync = true;
        VerticalOffset = offset;
        suppressOffsetSync = false;

        return bodyScroll.ScrollToAsync(0, offset, animated);
    }

    private enum TypingDragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
    }

    // Resize corner size, shrunk on small blocks so the middle third always stays a move grip —
    // with the fixed 24px zone a short appointment was resize-only (the corners covered all of it).
    private static float CornerZone(RectF rect)
        => MathF.Min(24f, MathF.Min(rect.Width, rect.Height) / 3f);

    private static DateTime ClampToDay(DateTime t, DateTime day, TimeSpan duration)
    {
        var start = day.Date;
        var end = start.AddDays(1) - duration;
        if (t < start)
        {
            return start;
        }

        if (t > end)
        {
            return end;
        }

        return t;
    }

    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldNotify)
        {
            oldNotify.CollectionChanged -= OnItemsCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newNotify)
        {
            newNotify.CollectionChanged += OnItemsCollectionChanged;
        }

        Rebuild();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnTypingItemChanged(ITypingScheduleItem? oldValue, ITypingScheduleItem? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnTypingItemPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnTypingItemPropertyChanged;
        }

        if (newValue is not null)
        {
            // Show: pop the bubble in (spring overshoot) from nothing.
            context.TypingItem = newValue;
            if (oldValue is null)
            {
                AnimateTypingBubble(0, 1, Easing.SpringOut, onFinished: null);
            }
            else
            {
                context.TypingScale = 1f;
                bodyCanvas.Invalidate();
            }
        }
        else if (oldValue is not null)
        {
            // Dismiss: keep drawing the outgoing item while it shrinks away, then clear it.
            AnimateTypingBubble(context.TypingScale, 0, Easing.CubicIn, onFinished: () =>
            {
                context.TypingItem = null;
                context.TypingScale = 1f;
                bodyCanvas.Invalidate();
            });
        }
        else
        {
            bodyCanvas.Invalidate();
        }
    }

    private void AnimateTypingBubble(double from, double to, Easing easing, Action? onFinished)
    {
        this.AbortAnimation("typing-bubble");
        var animation = new Animation(
            v =>
            {
                context.TypingScale = (float)v;
                bodyCanvas.Invalidate();
            },
            from,
            to,
            easing);
        animation.Commit(this, "typing-bubble", length: 260, finished: (_, _) => onFinished?.Invoke());
    }

    private void OnTypingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITypingScheduleItem.Start)
            || e.PropertyName == nameof(ITypingScheduleItem.End)
            || e.PropertyName == nameof(ITypingScheduleItem.PersonId)
            || e.PropertyName == nameof(ITypingScheduleItem.Title)
            || e.PropertyName == nameof(ITypingScheduleItem.Color)
            || string.IsNullOrEmpty(e.PropertyName))
        {
            bodyCanvas.Invalidate();
        }
    }

    private void OnHoldingScheduleChanged(IScheduleItem? oldValue, IScheduleItem? newValue)
    {
        if (oldValue is INotifyPropertyChanged oldInpc)
        {
            oldInpc.PropertyChanged -= OnHoldingItemPropertyChanged;
        }

        if (newValue is INotifyPropertyChanged newInpc)
        {
            newInpc.PropertyChanged += OnHoldingItemPropertyChanged;
        }

        // New item starts at its natural position; cancel any in-flight drag.
        holdingDragMode = TypingDragMode.None;
        holdingDragColumn = -1;
        context.HoldingItem = newValue;
        context.HoldingDragColumn = -1;
        context.HoldingDragStart = null;
        context.HoldingDragEnd = null;
        bodyScroll.Orientation = ScrollOrientation.Vertical;
        bodyCanvas.Invalidate();
    }

    private void OnHoldingItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => bodyCanvas.Invalidate();

    private void OnRendererChanged()
    {
        var renderer = Renderer;
        bodyDrawable.Renderer = renderer;
        headerDrawable.Renderer = renderer;
        allDayDrawable.Renderer = renderer;
        headerCanvas.Invalidate();
        allDayCanvas.Invalidate();
        bodyCanvas.Invalidate();
        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void OnAllDayTapped(object? sender, TappedEventArgs e)
    {
        var pt = e.GetPosition(allDayCanvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        foreach (var (item, rect) in allDayDrawable.HitMap)
        {
            if (rect.Contains(p))
            {
                ItemTapped?.Invoke(this, new ScheduleItemTappedEventArgs(item));
                return;
            }
        }
    }

    // The user (or momentum) scrolled the body: remember the offset and publish it once
    // scrolling settles. Synced siblings aren't visible mid-scroll, so per-frame publication
    // only cost main-thread time (binding fan-out + native re-scrolls on every live page).
    private void OnBodyScrolled(double scrollY)
    {
        if (suppressOffsetSync)
        {
            return;
        }

        pendingOffset = scrollY;
        offsetPublishTimer.Stop();
        offsetPublishTimer.Start();
    }

    private void PublishPendingOffset()
    {
        if (pendingOffset is not { } offset)
        {
            return;
        }

        pendingOffset = null;
        suppressOffsetSync = true;
        VerticalOffset = offset;
        suppressOffsetSync = false;
    }

    // VerticalOffset changed (typically pushed in from a bound sibling page): scroll to match.
    private void ApplyVerticalOffset(double offset)
    {
        if (suppressOffsetSync)
        {
            return;
        }

        if (Math.Abs(bodyScroll.ScrollY - offset) < 0.5)
        {
            return;
        }

        suppressOffsetSync = true;
        _ = bodyScroll.ScrollToAsync(0, offset, false);
        suppressOffsetSync = false;
    }

    // Coalesced entry point: any number of property changes within one UI cycle produce a
    // single rebuild pass on the next dispatcher drain.
    private void Rebuild()
    {
        if (rebuildQueued)
        {
            return;
        }

        rebuildQueued = true;
        Dispatcher.Dispatch(() =>
        {
            rebuildQueued = false;
            RebuildNow();
        });
    }

    // Runs a queued rebuild immediately; for code paths that read the rebuilt state
    // (context.Scale, columns) right after setting properties.
    private void FlushPendingRebuild()
    {
        if (!rebuildQueued)
        {
            return;
        }

        rebuildQueued = false;
        RebuildNow();
    }

    private void RebuildNow()
    {
        long started = ScheduleDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

        var theme = Theme;
        var personsMode = Persons is not null && Persons.Count > 0;

        var rangeStart = StartDay.Date;
        int days = ScheduleColumnBuilder.EffectiveDays(StartDay, EndDay, ViewMode);

        // Context carries the bar height; whether the in-house canvas shows it is a separate rule,
        // so a linked header can still render single-day columns the in-house bar would hide.
        float headerHeight = (float)theme.HeaderHeight;
        context.Theme = theme;
        context.TimeRailWidth = (float)theme.TimeRailWidth;
        context.HeaderHeight = headerHeight;
        context.Scale = new TimeScale((float)HourHeight, (float)TopContentInset, (float)BottomContentInset);

        bool inhouseHeader = HeaderMode == ScheduleHeaderMode.Inhouse && (personsMode || days > 1);
        headerCanvas.HeightRequest = inhouseHeader ? headerHeight : 0;
        headerCanvas.IsVisible = inhouseHeader;

        // Explicit row height, not Auto: Rebuild() is debounced, so the header GraphicsView's
        // HeightRequest is applied after a carousel cell's first measure. An Auto row would have
        // already measured the (then zero-desired) canvas to 0 and never re-measure until a relayout,
        // making the header vanish. The intended height is known here, so pin the row to it.
        headerRow.Height = inhouseHeader ? new GridLength(headerHeight) : new GridLength(0);
        bodyCanvas.HeightRequest = context.Scale.TotalHeight;

        var items = new List<IScheduleItem>();
        if (ItemsSource is not null)
        {
            foreach (var raw in ItemsSource)
            {
                if (raw is IScheduleItem si)
                {
                    items.Add(si);
                }
            }
        }

        context.Columns = ScheduleColumnBuilder.Build(StartDay, EndDay, ViewMode, Persons, items);
        context.Now = DateTime.Now;

        // All-day / cross-date items go in the panel above the grid, spanning the days they cover.
        var bars = AllDayLayout.Layout(items.Where(AllDayLayout.IsSpanning), DateOnly.FromDateTime(rangeStart), days);
        int laneCount = 0;
        foreach (var bar in bars)
        {
            laneCount = Math.Max(laneCount, bar.Lane + 1);
        }

        context.AllDayBars = bars;
        context.DayCount = days;
        context.AllDayLaneHeight = AllDayLaneHeight;

        // In Linked mode the external header hosts the all-day panel too.
        bool inhouseAllDay = HeaderMode != ScheduleHeaderMode.Linked;
        float panelHeight = laneCount > 0 ? (laneCount * AllDayLaneHeight) + 6f : 0f;
        allDayCanvas.HeightRequest = inhouseAllDay ? panelHeight : 0;
        allDayCanvas.IsVisible = inhouseAllDay && panelHeight > 0;

        // Explicit height for the same reason as headerRow above: an Auto row over a GraphicsView
        // collapses to 0 when the debounced Rebuild applies HeightRequest after the first measure.
        allDayRow.Height = allDayCanvas.IsVisible ? new GridLength(panelHeight) : new GridLength(0);

        headerCanvas.Invalidate();
        allDayCanvas.Invalidate();
        bodyCanvas.Invalidate();
        Rebuilt?.Invoke(this, EventArgs.Empty);

        if (started != 0)
        {
            var ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            ScheduleDiagnostics.Log($"rebuild #{++rebuildCount} {ms:F1}ms items={items.Count} days={days} cols={context.Columns.Count} h={context.Scale.TotalHeight:F0}");
        }

        // The canvas height may have changed (zoom, day/theme switch) without a new offset value,
        // so a synced sibling keeps a stale or clamped ScrollY. Re-seat the bound offset once the
        // ScrollView has remeasured (same defer as the Loaded jump). Skip while the user is
        // actively scrolling (a publish is pending) — the bound value is stale then by design.
        if (pendingOffset is null && VerticalOffset > 0 && Math.Abs(bodyScroll.ScrollY - VerticalOffset) >= 0.5)
        {
            Dispatcher.Dispatch(() => ApplyVerticalOffset(VerticalOffset));
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(bodyCanvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);

        if (TryBeginHoldingDrag(p))
        {
            longPressTimer.Stop();
            pointerDown = false;
            return;
        }

        if (TryBeginTypingDrag(p))
        {
            longPressTimer.Stop();
            pointerDown = false;
            return;
        }

        pointerDownPoint = p;
        pointerDown = true;
        longTapFired = false;
        moveCanceledTap = false;
        longPressTimer.Stop();
        longPressTimer.Start();
    }

    private bool TryBeginTypingDrag(PointF p)
    {
        var typing = TypingItem;
        if (typing is null)
        {
            return false;
        }

        var rect = bodyDrawable.TypingRect;
        if (rect is null || !rect.Value.Contains(p))
        {
            return false;
        }

        float cornerZone = CornerZone(rect.Value);
        float relX = p.X - rect.Value.X;
        float relY = p.Y - rect.Value.Y;
        bool inTopLeft = relX < cornerZone && relY < cornerZone;
        bool inBottomRight = (rect.Value.Width - relX) < cornerZone
            && (rect.Value.Height - relY) < cornerZone;

        if (inTopLeft)
        {
            typingDragMode = TypingDragMode.ResizeStart;
        }
        else if (inBottomRight)
        {
            typingDragMode = TypingDragMode.ResizeEnd;
        }
        else
        {
            typingDragMode = TypingDragMode.Move;
        }

        typingOriginPoint = p;
        typingOriginStart = typing.Start;
        typingOriginEnd = typing.End;
        SetBodyScrollLocked(true);
        return true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(bodyCanvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);

        if (holdingDragMode != TypingDragMode.None)
        {
            UpdateHoldingDrag(p);
            return;
        }

        if (typingDragMode != TypingDragMode.None)
        {
            UpdateTypingDrag(p);
            return;
        }

        if (!pointerDown)
        {
            return;
        }

        // Late begin: the press may have raced the holding block's first draw (no rect yet at
        // press time). Anchor the drag at the press point so the grab offset stays correct.
        if (TryBeginHoldingDrag(pointerDownPoint))
        {
            longPressTimer.Stop();
            pointerDown = false;
            UpdateHoldingDrag(p);
            return;
        }

        if (Math.Abs(p.X - pointerDownPoint.X) > LongPressMoveThreshold
            || Math.Abs(p.Y - pointerDownPoint.Y) > LongPressMoveThreshold)
        {
            moveCanceledTap = true;
            longPressTimer.Stop();
        }
    }

    private void UpdateTypingDrag(PointF p)
    {
        var typing = TypingItem;
        if (typing is null)
        {
            return;
        }

        double dy = p.Y - typingOriginPoint.Y;
        double snappedMinutes = Math.Round(dy / HourHeight * 60.0 / 15.0) * 15.0;
        var duration = typingOriginEnd - typingOriginStart;

        switch (typingDragMode)
        {
            case TypingDragMode.Move:
            {
                int targetCol = HitTestColumn(p.X);
                DateTime baseDay = targetCol >= 0
                    ? context.Columns[targetCol].DayStart
                    : typingOriginStart.Date;
                var tentative = baseDay.Add(typingOriginStart.TimeOfDay).AddMinutes(snappedMinutes);
                var clamped = ClampToDay(tentative, baseDay, duration);
                typing.Start = clamped;
                typing.End = clamped + duration;

                if (targetCol >= 0)
                {
                    var col = context.Columns[targetCol];
                    if (!string.Equals(typing.PersonId, col.PersonId, StringComparison.Ordinal)
                        && col.PersonId is not null)
                    {
                        typing.PersonId = col.PersonId;
                    }
                }

                break;
            }

            case TypingDragMode.ResizeStart:
            {
                var newStart = typingOriginStart.AddMinutes(snappedMinutes);
                var dayStart = typingOriginStart.Date;
                if (newStart < dayStart)
                {
                    newStart = dayStart;
                }

                if (newStart > typingOriginEnd.AddMinutes(-15))
                {
                    newStart = typingOriginEnd.AddMinutes(-15);
                }

                typing.Start = newStart;
                break;
            }

            case TypingDragMode.ResizeEnd:
            {
                var newEnd = typingOriginEnd.AddMinutes(snappedMinutes);
                var minEnd = typingOriginStart.AddMinutes(15);
                if (newEnd < minEnd)
                {
                    newEnd = minEnd;
                }

                var dayEnd = typingOriginStart.Date.AddDays(1);
                if (newEnd > dayEnd)
                {
                    newEnd = dayEnd;
                }

                typing.End = newEnd;
                break;
            }
        }
    }

    private int HitTestColumn(float x)
    {
        int n = context.Columns.Count;
        if (n == 0)
        {
            return -1;
        }

        float railWidth = context.TimeRailWidth;
        float width = (float)bodyCanvas.Width;
        if (width <= railWidth || x < railWidth)
        {
            return -1;
        }

        float colW = (width - railWidth) / n;
        if (colW <= 0)
        {
            return -1;
        }

        int idx = (int)Math.Floor((x - railWidth) / colW);
        return Math.Clamp(idx, 0, n - 1);
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (holdingDragMode != TypingDragMode.None)
        {
            EndHoldingDrag(drop: true);
            return;
        }

        if (typingDragMode != TypingDragMode.None)
        {
            EndTypingDrag();
            return;
        }

        if (!pointerDown)
        {
            return;
        }

        longPressTimer.Stop();
        pointerDown = false;

        if (longTapFired || moveCanceledTap)
        {
            return;
        }

        DispatchTapAt(pointerDownPoint, longPress: false);
    }

    private void OnPointerCanceled(object? sender, PointerEventArgs e)
    {
        if (holdingDragMode != TypingDragMode.None)
        {
            EndHoldingDrag(drop: false);
        }

        if (typingDragMode != TypingDragMode.None)
        {
            EndTypingDrag();
        }

        CancelPendingLongPress();
    }

    private void EndTypingDrag()
    {
        typingDragMode = TypingDragMode.None;
        SetBodyScrollLocked(false);
    }

    // Locks/unlocks body scrolling around a block drag. Orientation alone only applies after an
    // async layout pass, so a quick press-and-drag lost the race: the platform pan recognizer still
    // engaged, cancelled our pointer gesture and scrolled instead (the drag only survived when the
    // finger held still long enough for the pass to land). Disable the platform scroll synchronously.
    private void SetBodyScrollLocked(bool locked)
    {
        bodyScroll.Orientation = locked ? ScrollOrientation.Neither : ScrollOrientation.Vertical;

#if IOS
        if (bodyScroll.Handler?.PlatformView is UIKit.UIScrollView nativeScroll)
        {
            nativeScroll.ScrollEnabled = !locked;
        }
#elif ANDROID
        if (locked && bodyCanvas.Handler?.PlatformView is Android.Views.View nativeCanvas)
        {
            nativeCanvas.Parent?.RequestDisallowInterceptTouchEvent(true);
        }
#endif
    }

    private bool TryBeginHoldingDrag(PointF p)
    {
        var item = HoldingSchedule;
        if (item is null)
        {
            return false;
        }

        var rect = bodyDrawable.HoldingRect;

        // A small halo around the block so grabbing it doesn't demand pixel accuracy.
        if (rect is null || !rect.Value.Inflate(16, 16).Contains(p))
        {
            return false;
        }

        // Top-left corner resizes the start, bottom-right resizes the end, elsewhere moves.
        float cornerZone = CornerZone(rect.Value);
        float relX = p.X - rect.Value.X;
        float relY = p.Y - rect.Value.Y;
        bool inTopLeft = relX < cornerZone && relY < cornerZone;
        bool inBottomRight = (rect.Value.Width - relX) < cornerZone && (rect.Value.Height - relY) < cornerZone;
        holdingDragMode = inTopLeft ? TypingDragMode.ResizeStart
            : inBottomRight ? TypingDragMode.ResizeEnd
            : TypingDragMode.Move;

        holdingOriginPoint = p;
        holdingOriginStart = item.Start;
        holdingOriginEnd = item.End;
        holdingDragStart = item.Start;
        holdingDragEnd = item.End;
        holdingDragColumn = HitTestColumn(p.X);
        context.HoldingDragColumn = holdingDragColumn;
        context.HoldingDragStart = holdingDragStart;
        context.HoldingDragEnd = holdingDragEnd;
        SetBodyScrollLocked(true);
        bodyCanvas.Invalidate();
        return true;
    }

    private void UpdateHoldingDrag(PointF p)
    {
        var item = HoldingSchedule;
        if (item is null)
        {
            return;
        }

        double dy = p.Y - holdingOriginPoint.Y;
        double snappedMinutes = Math.Round(dy / HourHeight * 60.0 / 15.0) * 15.0;
        var duration = holdingOriginEnd - holdingOriginStart;
        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(30);
        }

        switch (holdingDragMode)
        {
            case TypingDragMode.Move:
            {
                // Free vertical (snapped), horizontal snaps to the nearest column.
                int col = HitTestColumn(p.X);
                DateTime baseDay = col >= 0 ? context.Columns[col].DayStart : holdingOriginStart.Date;
                var tentative = baseDay.Add(holdingOriginStart.TimeOfDay).AddMinutes(snappedMinutes);
                var clamped = ClampToDay(tentative, baseDay, duration);
                holdingDragStart = clamped;
                holdingDragEnd = clamped + duration;
                holdingDragColumn = col;
                break;
            }

            case TypingDragMode.ResizeStart:
            {
                var newStart = holdingOriginStart.AddMinutes(snappedMinutes);
                var dayStart = holdingOriginStart.Date;
                if (newStart < dayStart)
                {
                    newStart = dayStart;
                }

                if (newStart > holdingOriginEnd.AddMinutes(-15))
                {
                    newStart = holdingOriginEnd.AddMinutes(-15);
                }

                holdingDragStart = newStart;
                holdingDragEnd = holdingOriginEnd;
                break;
            }

            case TypingDragMode.ResizeEnd:
            {
                var newEnd = holdingOriginEnd.AddMinutes(snappedMinutes);
                var minEnd = holdingOriginStart.AddMinutes(15);
                if (newEnd < minEnd)
                {
                    newEnd = minEnd;
                }

                var dayEnd = holdingOriginStart.Date.AddDays(1);
                if (newEnd > dayEnd)
                {
                    newEnd = dayEnd;
                }

                holdingDragStart = holdingOriginStart;
                holdingDragEnd = newEnd;
                break;
            }
        }

        context.HoldingDragColumn = holdingDragColumn;
        context.HoldingDragStart = holdingDragStart;
        context.HoldingDragEnd = holdingDragEnd;
        bodyCanvas.Invalidate();
    }

    private void EndHoldingDrag(bool drop)
    {
        var item = HoldingSchedule;
        holdingDragMode = TypingDragMode.None;
        SetBodyScrollLocked(false);

        if (drop && item is not null)
        {
            string? personId = holdingDragColumn >= 0 ? context.Columns[holdingDragColumn].PersonId : null;
            HoldingDropped?.Invoke(this, new HoldingDroppedEventArgs(item, holdingDragStart, holdingDragEnd, personId));
        }

        // Event-only: don't mutate the item — return the block to its natural position.
        holdingDragColumn = -1;
        context.HoldingDragColumn = -1;
        context.HoldingDragStart = null;
        context.HoldingDragEnd = null;
        bodyCanvas.Invalidate();
    }

#if IOS
    private void OnBodyCanvasHandlerChanged(object? sender, EventArgs e)
    {
        if (!iosMenuAttached && bodyCanvas.Handler?.PlatformView is UIKit.UIView view)
        {
            iosMenuAttached = true;
            menuPlatformState = QuickActionMenu.AttachIos(view, this);
        }
    }
#endif

    /// <summary>Returns the appointment whose rendered rect contains the given body-canvas point, or null.</summary>
    internal IScheduleItem? HitTestItem(PointF canvasPoint)
        => HitTestItemRect(canvasPoint)?.Item;

    /// <summary>Returns the appointment and its rendered rect at the given body-canvas point, or null.</summary>
    internal (IScheduleItem Item, RectF Rect)? HitTestItemRect(PointF canvasPoint)
    {
        foreach (var (item, rect) in bodyDrawable.HitMap)
        {
            if (rect.Contains(canvasPoint))
            {
                return (item, rect);
            }
        }

        return null;
    }

    /// <summary>Returns the menu actions for an appointment (empty when no provider or no actions).</summary>
    internal IReadOnlyList<ScheduleMenuAction> GetItemActions(IScheduleItem item)
        => ItemActionsProvider?.Invoke(item) ?? Array.Empty<ScheduleMenuAction>();

    /// <summary>Raises <see cref="ItemActionInvoked"/> for a chosen action.</summary>
    internal void RaiseItemAction(IScheduleItem item, string action)
        => ItemActionInvoked?.Invoke(this, new ScheduleItemActionEventArgs(item, action));

    // Shows the native long-press menu for an appointment. Returns true when it handled the press
    // (so the gesture's ItemLongTapped is suppressed). iOS uses a UIContextMenuInteraction wired up
    // separately, so here we only claim the press when there are actions to show.
    private bool TryShowItemMenu(IScheduleItem item, PointF point)
    {
#if IOS
        _ = point;
        return GetItemActions(item).Count > 0;
#elif ANDROID
        var actions = GetItemActions(item);
        if (actions.Count == 0)
        {
            return false;
        }

        return QuickActionMenu.ShowAndroid(bodyCanvas, point, actions, label => RaiseItemAction(item, label));
#else
        _ = item;
        _ = point;
        return false;
#endif
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        longPressTimer.Stop();
        if (!pointerDown || moveCanceledTap)
        {
            return;
        }

        longTapFired = true;
        DispatchTapAt(pointerDownPoint, longPress: true);
    }

    private void CancelPendingLongPress()
    {
        longPressTimer.Stop();
        pointerDown = false;
        longTapFired = false;
        moveCanceledTap = true;
    }

    private void DispatchTapAt(PointF point, bool longPress)
    {
        foreach (var (item, rect) in bodyDrawable.HitMap)
        {
            if (rect.Contains(point))
            {
                var args = new ScheduleItemTappedEventArgs(item);
                if (longPress)
                {
                    if (!TryShowItemMenu(item, point))
                    {
                        ItemLongTapped?.Invoke(this, args);
                    }
                }
                else
                {
                    ItemTapped?.Invoke(this, args);
                }

                return;
            }
        }

        if (TryGetTimeAt(point, out var when))
        {
            var args = new ScheduleTappedEventArgs(when);
            if (longPress)
            {
                LongTapped?.Invoke(this, args);
            }
            else
            {
                Tapped?.Invoke(this, args);
            }
        }
    }

    private bool TryGetTimeAt(PointF point, out DateTime when)
    {
        when = default;
        int n = context.Columns.Count;
        if (n == 0)
        {
            return false;
        }

        float railWidth = context.TimeRailWidth;
        float canvasWidth = (float)bodyCanvas.Width;
        if (canvasWidth <= railWidth || point.X < railWidth)
        {
            return false;
        }

        float colW = (canvasWidth - railWidth) / n;
        if (colW <= 0)
        {
            return false;
        }

        int idx = (int)Math.Floor((point.X - railWidth) / colW);
        idx = Math.Clamp(idx, 0, n - 1);
        var column = context.Columns[idx];
        var timeOfDay = context.Scale.TimeForY(point.Y);
        if (timeOfDay.TotalHours > 24)
        {
            timeOfDay = TimeSpan.FromHours(24);
        }

        when = column.DayStart + timeOfDay;
        return true;
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                CancelPendingLongPress();
                pinchBase = HourHeight;
                pinchScale = 1.0;
                pinchAnchorHours = -1;
                break;

            case GestureStatus.Running:
                if (pinchAnchorHours < 0)
                {
                    CaptureAnchor(e.ScaleOrigin.Y);
                }

                pinchScale *= e.Scale;
                var target = Math.Clamp(pinchBase * pinchScale, 24, 200);
                if (Math.Abs(target - context.Scale.HourHeight) >= 1.0)
                {
                    context.Scale = new TimeScale((float)target, context.Scale.TopPadding, context.Scale.BottomPadding);
                    bodyCanvas.HeightRequest = context.Scale.TotalHeight;
                    bodyCanvas.Invalidate();
                    KeepAnchorPinned();
                }

                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var final = Math.Clamp(pinchBase * pinchScale, 24, 200);
                pinchScale = 1.0;
                pinchAnchorHours = -1;
                HourHeight = final;
                break;
        }
    }

    private void CaptureAnchor(double originFraction)
    {
        var totalHeight = context.Scale.TotalHeight;
        var anchorCanvasY = originFraction * totalHeight;
        pinchAnchorHours = context.Scale.TimeForY((float)anchorCanvasY).TotalHours;
        pinchAnchorViewportY = anchorCanvasY - bodyScroll.ScrollY;
    }

    private void KeepAnchorPinned()
    {
        var newAnchorCanvasY = context.Scale.YForTime(TimeSpan.FromHours(pinchAnchorHours));
        var newScrollY = newAnchorCanvasY - pinchAnchorViewportY;
        var maxScroll = Math.Max(0, context.Scale.TotalHeight - bodyScroll.Height);
        newScrollY = Math.Clamp(newScrollY, 0, maxScroll);
        _ = bodyScroll.ScrollToAsync(0, newScrollY, false);
    }
}
