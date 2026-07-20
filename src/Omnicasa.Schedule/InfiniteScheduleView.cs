#if ANDROID || IOS
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Omnicasa.Schedule;

/// <summary>Payload for <c>InfiniteScheduleView.VisibleRangeChanged</c>: the visible day window.</summary>
public sealed class ScheduleVisibleRangeChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleVisibleRangeChangedEventArgs"/> class.</summary>
    /// <param name="firstDay">First (leftmost) visible day.</param>
    /// <param name="lastDay">Last visible day of the primary window.</param>
    public ScheduleVisibleRangeChangedEventArgs(DateTime firstDay, DateTime lastDay)
    {
        FirstDay = firstDay;
        LastDay = lastDay;
    }

    /// <summary>Gets the first (leftmost) visible day.</summary>
    public DateTime FirstDay { get; }

    /// <summary>Gets the last visible day of the primary <c>ViewMode</c>-day window.</summary>
    public DateTime LastDay { get; }
}

/// <summary>
/// A single-canvas, GPU-rendered schedule whose day axis scrolls infinitely. Unlike a carousel of
/// per-day <see cref="ScheduleView"/> pages, this draws every visible day onto one
/// <see cref="SKGLView"/> and owns a virtual horizontal offset (in logical pixels from
/// <see cref="AnchorDay"/>), so there is no page count and no native scroll rect to run out of.
/// (SKGLView GL on the iOS simulator can be unreliable; test on a real device.)
///
/// <para>Rendering uses snapshot-and-translate: a buffer of day columns is recorded once into an
/// <see cref="SKPicture"/> in content space; panning just replays that picture translated by the
/// offset, and the picture is only re-recorded when the visible window nears the buffer edge. That
/// keeps per-frame cost flat (a screenful of days) the way native calendars do with tiled surfaces.</para>
///
/// <para>Wired: body grid, day/person header, hour rail, event blocks, tap/long-press, momentum
/// scroll with day snap, typing &amp; holding drags, per-person sub-columns, custom
/// <see cref="SkiaRenderer"/>, two-finger pinch-to-zoom (HourHeight), and the all-day panel
/// (<see cref="ShowAllDay"/>). Not yet: the quick-action menu and Linked/None header modes.</para>
/// </summary>
public sealed class InfiniteScheduleView : ContentView
{
    /// <summary>Extra day columns recorded on each side of the visible window before a re-record is needed.</summary>
    private const int BufferDays = 6;

    /// <summary>Speed below which a fling is treated as stopped (logical px/s).</summary>
    private const double FlingStopSpeed = 30;

    /// <summary>Time constant (s) of the critically damped spring that settles the horizontal offset.</summary>
    private const double SettleTau = 0.11;

    /// <summary>Weight of the newest sample in the release-velocity moving average (0..1).</summary>
    private const double VelocitySmoothing = 0.5;

    /// <summary>Finger travel (logical px) before a drag commits to a single axis.</summary>
    private const double PanLockThreshold = 10;

    /// <summary>Minute granularity that block drags/resizes snap to.</summary>
    private const double SnapMinutes = 15;

    /// <summary>Grab halo (logical px) around a holding block, so it doesn't demand pixel accuracy.</summary>
    private const float HoldingHalo = 16;

    /// <summary>Finger speed (logical px/s) that counts as a flick and advances an extra day.</summary>
    private const double PageFlickSpeed = 500;

    /// <summary>Duration (s) of the typing draft's pop-in animation.</summary>
    private const double TypingAppearDuration = 0.18;

    /// <summary>Height (logical px) of one lane in the all-day panel (matches <see cref="ScheduleView"/>).</summary>
    private const float AllDayLaneHeight = 22f;

    /// <summary>Breathing room below the last all-day lane before the time grid starts.</summary>
    private const float AllDayPanelPadding = 6f;

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnDataChanged());

    /// <summary>Bindable property for <see cref="ViewMode"/>.</summary>
    public static readonly BindableProperty ViewModeProperty =
        BindableProperty.Create(
            nameof(ViewMode),
            typeof(int),
            typeof(InfiniteScheduleView),
            1,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnLayoutChanged(),
            coerceValue: (_, v) => Math.Clamp((int)v, 1, 7));

    /// <summary>Bindable property for <see cref="ShowAllDay"/>.</summary>
    public static readonly BindableProperty ShowAllDayProperty =
        BindableProperty.Create(
            nameof(ShowAllDay),
            typeof(bool),
            typeof(InfiniteScheduleView),
            true,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnDataChanged());

    /// <summary>Bindable property for <see cref="HourHeight"/>.</summary>
    public static readonly BindableProperty HourHeightProperty =
        BindableProperty.Create(
            nameof(HourHeight),
            typeof(double),
            typeof(InfiniteScheduleView),
            60.0,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnLayoutChanged(),
            coerceValue: (_, v) => Math.Clamp((double)v, 24.0, 300.0));

    /// <summary>Bindable property for <see cref="AnchorDay"/>.</summary>
    public static readonly BindableProperty AnchorDayProperty =
        BindableProperty.Create(
            nameof(AnchorDay),
            typeof(DateTime),
            typeof(InfiniteScheduleView),
            DateTime.Today,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnDataChanged());

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleViewTheme),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).OnLayoutChanged());

    /// <summary>Bindable property for <see cref="MinDay"/>.</summary>
    public static readonly BindableProperty MinDayProperty =
        BindableProperty.Create(
            nameof(MinDay),
            typeof(DateTime?),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).Invalidate());

    /// <summary>Bindable property for <see cref="MaxDay"/>.</summary>
    public static readonly BindableProperty MaxDayProperty =
        BindableProperty.Create(
            nameof(MaxDay),
            typeof(DateTime?),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).Invalidate());

    /// <summary>Bindable property for <see cref="CurrentDay"/>.</summary>
    public static readonly BindableProperty CurrentDayProperty =
        BindableProperty.Create(
            nameof(CurrentDay),
            typeof(DateTime),
            typeof(InfiniteScheduleView),
            DateTime.Today,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, n) => ((InfiniteScheduleView)b).OnCurrentDayChanged((DateTime)n));

    /// <summary>Bindable property for <see cref="FlingGain"/>.</summary>
    public static readonly BindableProperty FlingGainProperty =
        BindableProperty.Create(
            nameof(FlingGain),
            typeof(double),
            typeof(InfiniteScheduleView),
            1.8,
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    /// <summary>Bindable property for <see cref="FlingDecelerationTime"/>.</summary>
    public static readonly BindableProperty FlingDecelerationTimeProperty =
        BindableProperty.Create(
            nameof(FlingDecelerationTime),
            typeof(double),
            typeof(InfiniteScheduleView),
            0.5,
            coerceValue: (_, v) => Math.Clamp((double)v, 0.05, 5.0));

    /// <summary>Bindable property for <see cref="MaxFlingSpeed"/>.</summary>
    public static readonly BindableProperty MaxFlingSpeedProperty =
        BindableProperty.Create(
            nameof(MaxFlingSpeed),
            typeof(double),
            typeof(InfiniteScheduleView),
            12000.0,
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    // --- ScheduleView API parity (same names so clients migrate by swapping the type) ---

    /// <summary>Bindable property for <see cref="VerticalOffset"/>.</summary>
    public static readonly BindableProperty VerticalOffsetProperty =
        BindableProperty.Create(
            nameof(VerticalOffset),
            typeof(double),
            typeof(InfiniteScheduleView),
            0.0,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, n) => ((InfiniteScheduleView)b).OnVerticalOffsetChanged((double)n));

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(ScheduleViewRenderer),
            typeof(InfiniteScheduleView),
            null);

    /// <summary>Bindable property for <see cref="Persons"/>.</summary>
    public static readonly BindableProperty PersonsProperty =
        BindableProperty.Create(
            nameof(Persons),
            typeof(IList<IPerson>),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).MarkStripDirty());

    /// <summary>Bindable property for <see cref="TypingItem"/>.</summary>
    public static readonly BindableProperty TypingItemProperty =
        BindableProperty.Create(
            nameof(TypingItem),
            typeof(ITypingScheduleItem),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, o, n) => ((InfiniteScheduleView)b).OnTypingItemChanged(o, n));

    /// <summary>Bindable property for <see cref="HoldingSchedule"/>.</summary>
    public static readonly BindableProperty HoldingScheduleProperty =
        BindableProperty.Create(
            nameof(HoldingSchedule),
            typeof(IScheduleItem),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).Invalidate());

    /// <summary>Bindable property for <see cref="HeaderMode"/>.</summary>
    public static readonly BindableProperty HeaderModeProperty =
        BindableProperty.Create(
            nameof(HeaderMode),
            typeof(ScheduleHeaderMode),
            typeof(InfiniteScheduleView),
            ScheduleHeaderMode.Inhouse,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).Invalidate());

    /// <summary>Bindable property for <see cref="TopContentInset"/>.</summary>
    public static readonly BindableProperty TopContentInsetProperty =
        BindableProperty.Create(
            nameof(TopContentInset),
            typeof(double),
            typeof(InfiniteScheduleView),
            0.0,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).MarkStripDirty(),
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    /// <summary>Bindable property for <see cref="BottomContentInset"/>.</summary>
    public static readonly BindableProperty BottomContentInsetProperty =
        BindableProperty.Create(
            nameof(BottomContentInset),
            typeof(double),
            typeof(InfiniteScheduleView),
            0.0,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).MarkStripDirty(),
            coerceValue: (_, v) => Math.Max(0.0, (double)v));

    /// <summary>Bindable property for <see cref="SkiaRenderer"/>.</summary>
    public static readonly BindableProperty SkiaRendererProperty =
        BindableProperty.Create(
            nameof(SkiaRenderer),
            typeof(InfiniteScheduleRenderer),
            typeof(InfiniteScheduleView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteScheduleView)b).MarkStripDirty());

    /// <summary>Bindable property for <see cref="PagingEnabled"/>.</summary>
    public static readonly BindableProperty PagingEnabledProperty =
        BindableProperty.Create(
            nameof(PagingEnabled),
            typeof(bool),
            typeof(InfiniteScheduleView),
            true);

    private readonly ScheduleViewTheme fallbackTheme = new ScheduleViewTheme();

    private readonly SKGLView canvas;

    private readonly Stopwatch clock = Stopwatch.StartNew();

    // Item hit rectangles in strip-local content coordinates, rebuilt with the strip.
    private readonly List<(IScheduleItem Item, SKRect Rect)> hitMap = new List<(IScheduleItem, SKRect)>();

    // All-day bar hit rectangles in view coordinates; the panel is painted live, not into the strip.
    private readonly List<(IScheduleItem Item, SKRect Rect)> allDayHitMap = new List<(IScheduleItem, SKRect)>();

    // Active touch points by id, so two-finger pinch-to-zoom can be tracked.
    private readonly Dictionary<long, Point> touches = new Dictionary<long, Point>();

    // Items indexed by the day(s) they touch, so a strip re-record only scans the visible window.
    private readonly Dictionary<DateOnly, List<IScheduleItem>> itemsByDay = new Dictionary<DateOnly, List<IScheduleItem>>();

    private List<IScheduleItem> items = new List<IScheduleItem>();

    // Owned scroll state, in logical pixels. Horizontal is measured from AnchorDay's left edge.
    private double horizontalOffset;

    private double scrollY = double.NaN;

    private float dayWidth;

    // Device-pixel → logical ratio, taken from the paint pass; used to convert touch coordinates.
    private float touchScale = 1;

    // Start time of the typing draft's pop-in (NaN when not animating); set to request another frame.
    private double typingAppearStart = double.NaN;

    private bool overlayNeedsFrame;

    // Last-reported leftmost visible day index (relative to AnchorDay), to fire VisibleRangeChanged once per day.
    private int? reportedFirstDay;

    private bool suppressCurrentDaySync;

    // Recorded day-column buffer (content space) and the day index it starts at.
    private SKPicture? strip;

    private int stripStartDay;

    private int stripDayCount;

    // All-day bars for the recorded strip's day range, plus the panel height they imply.
    private IReadOnlyList<AllDayBar> allDayBars = Array.Empty<AllDayBar>();

    private float allDayHeight;

    private bool stripDirty = true;

    // Pan/fling bookkeeping.
    private double panStartHorizontal;

    private double panStartVertical;

    private double lastPanTotalX;

    private double lastPanTotalY;

    private double panSampleSeconds;

    private double velocityX;

    private double velocityY;

    private bool panning;

    // Axis the current drag is locked to: 0 = undecided, 1 = horizontal, 2 = vertical.
    private int lockAxis;

    private bool flinging;

    // True while a press has "caught" a running fling — suppresses the tap action on release.
    private bool caughtMotion;

    // Two-finger pinch-to-zoom state (zooms HourHeight around the pinch center).
    private bool inPinch;

    private double pinchStartDist;

    private double pinchStartHourHeight;

    private double pinchAnchorHours;

    // Live zoom while pinching (NaN when idle) — display scales the cached strip instead of
    // re-recording it every frame; the committed HourHeight only changes once, at pinch end.
    private double liveHourHeight = double.NaN;

    // HourHeight the current strip picture was recorded at (for the pinch vertical-scale factor).
    private float stripHourHeight = 60;

    private bool settling;

    private double settleTargetH;

    private double lastFrameSeconds;

    // True while we publish VerticalOffset ourselves, to swallow the resulting property-changed echo.
    private bool suppressOffsetSync;

    private bool longFired;

    // Press-and-hold tracking for tap / long-tap.
    private Point pressPoint;

    private bool pressValid;

    private IDispatcherTimer? longPressTimer;
    private IDispatcherTimer? nowTimer;

    // Block-drag state (TypingItem move/resize, HoldingSchedule move/resize).
    private InteractionKind interaction;

    private BlockDragMode blockMode;

    private Point dragStartPoint;

    private DateTime dragOriginStart;

    private DateTime dragOriginEnd;

    private DateTime holdStart;

    private DateTime holdEnd;

    private string? holdPersonId;

    private string? dragOriginPersonId;

    private bool holdingActive;

#if IOS
    // Rooted delegate for the native context-menu interaction.
    private object? iosMenu;
#endif

    /// <summary>Initializes a new instance of the <see cref="InfiniteScheduleView"/> class.</summary>
    public InfiniteScheduleView()
    {
        canvas = new SKGLView { HasRenderLoop = false, EnableTouchEvents = true };
        canvas.PaintSurface += OnPaintSurface;

        // One unified touch stream drives scroll, fling, tap/long-press, and block drags — no MAUI
        // gesture recognizers, so there's no scroll-vs-tap-vs-drag arbitration race.
        canvas.Touch += OnTouch;

        Content = canvas;
        SizeChanged += (_, _) => OnLayoutChanged();

        // Advance the current-time marker while the view is on screen (it moves ~1px/min).
        Loaded += (_, _) =>
        {
            StartNowTimer();
            Invalidate();
        };
        Unloaded += (_, _) => StopNowTimer();

        // The GL surface can be torn down while the view is off-screen (tab switch / memory pressure)
        // and, with HasRenderLoop off, stays blank until the next data/scroll event. Repaint the cached
        // frame whenever the native canvas re-attaches so it comes straight back on reopen instead of
        // after the next fetch (a few seconds). Handler goes null on detach — only repaint on attach.
        canvas.HandlerChanged += (_, _) =>
        {
            if (canvas.Handler != null)
            {
                Invalidate();
            }
        };

#if IOS
        // Native UIContextMenuInteraction for the appointment quick-action menu (needs the platform view).
        canvas.HandlerChanged += (_, _) => AttachIosMenu();
#endif
    }

    /// <summary>Raised when empty grid space is tapped; payload carries the tapped date/time.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? Tapped;

    /// <summary>Raised when an appointment block is tapped.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemTapped;

    /// <summary>Raised when empty grid space is long-pressed; payload carries the date/time.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? LongTapped;

    /// <summary>Raised when an appointment block is long-pressed.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemLongTapped;

    /// <summary>Raised when a <see cref="HoldingSchedule"/> block is dropped, with the snapped time/person.</summary>
    public event EventHandler<HoldingDroppedEventArgs>? HoldingDropped;

    /// <summary>Raised when an action is chosen from an appointment's long-press quick-action menu.</summary>
    public event EventHandler<ScheduleItemActionEventArgs>? ItemActionInvoked;

    /// <summary>Raised when the visible day window changes (after scroll/fling/page settle and while crossing days).</summary>
    public event EventHandler<ScheduleVisibleRangeChangedEventArgs>? VisibleRangeChanged;

    private enum InteractionKind
    {
        None,
        Scroll,
        Typing,
        Holding,
    }

    private enum BlockDragMode
    {
        Move,
        ResizeStart,
        ResizeEnd,
    }

    /// <summary>Appointments to render. Items implement <see cref="IScheduleItem"/>.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Number of day columns visible at once (1..7); sets the day-column width.</summary>
    public int ViewMode
    {
        get => (int)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>
    /// Shows the all-day panel above the time grid for all-day and multi-day items. When off those
    /// items are not drawn at all, since the time grid deliberately excludes them.
    /// </summary>
    public bool ShowAllDay
    {
        get => (bool)GetValue(ShowAllDayProperty);
        set => SetValue(ShowAllDayProperty, value);
    }

    /// <summary>Vertical size of one hour, in logical pixels.</summary>
    public double HourHeight
    {
        get => (double)GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    /// <summary>The day mapped to horizontal offset 0; scrolling is measured relative to it.</summary>
    public DateTime AnchorDay
    {
        get => (DateTime)GetValue(AnchorDayProperty);
        set => SetValue(AnchorDayProperty, value);
    }

    /// <summary>Vertical scroll offset in logical pixels (time axis). Two-way; also driven by scrolling.</summary>
    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>Accepted for API parity; ignored on the SkiaSharp path — use <see cref="SkiaRenderer"/> instead.</summary>
    public ScheduleViewRenderer? Renderer
    {
        get => (ScheduleViewRenderer?)GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Skia-native painter for appointment / typing / holding blocks; null uses the built-in look.</summary>
    public InfiniteScheduleRenderer? SkiaRenderer
    {
        get => (InfiniteScheduleRenderer?)GetValue(SkiaRendererProperty);
        set => SetValue(SkiaRendererProperty, value);
    }

    /// <summary>When true (default), a horizontal swipe snaps one page (ViewMode days) at a time, carousel-style.</summary>
    public bool PagingEnabled
    {
        get => (bool)GetValue(PagingEnabledProperty);
        set => SetValue(PagingEnabledProperty, value);
    }

    /// <summary>Optional persons; when non-empty each day splits into one sub-column per person.</summary>
    public IList<IPerson>? Persons
    {
        get => (IList<IPerson>?)GetValue(PersonsProperty);
        set => SetValue(PersonsProperty, value);
    }

    /// <summary>Optional draft item overlay, for API parity. (Pending: move/resize drag not yet wired.)</summary>
    public ITypingScheduleItem? TypingItem
    {
        get => (ITypingScheduleItem?)GetValue(TypingItemProperty);
        set => SetValue(TypingItemProperty, value);
    }

    /// <summary>Optional held item overlay, for API parity. (Pending: holding drag not yet wired.)</summary>
    public IScheduleItem? HoldingSchedule
    {
        get => (IScheduleItem?)GetValue(HoldingScheduleProperty);
        set => SetValue(HoldingScheduleProperty, value);
    }

    /// <summary>Where the header is rendered; only <see cref="ScheduleHeaderMode.Inhouse"/> is wired so far.</summary>
    public ScheduleHeaderMode HeaderMode
    {
        get => (ScheduleHeaderMode)GetValue(HeaderModeProperty);
        set => SetValue(HeaderModeProperty, value);
    }

    /// <summary>Blank space above midnight inside the scrollable body, in logical pixels.</summary>
    public double TopContentInset
    {
        get => (double)GetValue(TopContentInsetProperty);
        set => SetValue(TopContentInsetProperty, value);
    }

    /// <summary>Blank space below the 24:00 line inside the scrollable body, in logical pixels.</summary>
    public double BottomContentInset
    {
        get => (double)GetValue(BottomContentInsetProperty);
        set => SetValue(BottomContentInsetProperty, value);
    }

    /// <summary>Supplies long-press menu actions per item, for API parity. (Pending: menu not yet wired.)</summary>
    public Func<IScheduleItem, IReadOnlyList<ScheduleMenuAction>>? ItemActionsProvider { get; set; }

    /// <summary>Optional visual theme; a default theme is used when null.</summary>
    public ScheduleViewTheme? Theme
    {
        get => (ScheduleViewTheme?)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Earliest reachable day, or null for unbounded scroll-back.</summary>
    public DateTime? MinDay
    {
        get => (DateTime?)GetValue(MinDayProperty);
        set => SetValue(MinDayProperty, value);
    }

    /// <summary>Latest reachable day, or null for unbounded scroll-forward.</summary>
    public DateTime? MaxDay
    {
        get => (DateTime?)GetValue(MaxDayProperty);
        set => SetValue(MaxDayProperty, value);
    }

    /// <summary>The leftmost visible day. Two-way: updates as you scroll/page, and setting it scrolls there.</summary>
    public DateTime CurrentDay
    {
        get => (DateTime)GetValue(CurrentDayProperty);
        set => SetValue(CurrentDayProperty, value);
    }

    /// <summary>Flick launch multiplier; higher = faster, further scrolling (default 1.8, min 0).</summary>
    public double FlingGain
    {
        get => (double)GetValue(FlingGainProperty);
        set => SetValue(FlingGainProperty, value);
    }

    /// <summary>Momentum glide time in seconds; higher = longer, slower-settling glide (default 0.5).</summary>
    public double FlingDecelerationTime
    {
        get => (double)GetValue(FlingDecelerationTimeProperty);
        set => SetValue(FlingDecelerationTimeProperty, value);
    }

    /// <summary>Upper bound on fling speed in logical px/s; caps very fast flicks (default 12000).</summary>
    public double MaxFlingSpeed
    {
        get => (double)GetValue(MaxFlingSpeedProperty);
        set => SetValue(MaxFlingSpeedProperty, value);
    }

    private ScheduleViewTheme ActiveTheme => Theme ?? fallbackTheme;

    private float RailWidth => (float)ActiveTheme.TimeRailWidth;

    private float HeaderHeight => (float)ActiveTheme.HeaderHeight;

    // Top of the time grid: below the header, and below the all-day panel when it has lanes.
    private float BodyTop => HeaderHeight + allDayHeight;

    private int PersonCount => Persons is { Count: > 0 } p ? p.Count : 1;

    private InfiniteScheduleRenderer ActiveRenderer => SkiaRenderer ?? InfiniteScheduleRenderer.Default;

    private double EffectiveHourHeight => double.IsNaN(liveHourHeight) ? HourHeight : liveHourHeight;

    /// <summary>
    /// Scrolls the body so <paramref name="timeOfDay"/> sits at the top edge and publishes
    /// <see cref="VerticalOffset"/>. The time→pixel conversion is done for you.
    /// </summary>
    /// <param name="timeOfDay">Target time of day to bring to the top of the viewport.</param>
    /// <param name="animated">Reserved for parity; the scroll is currently immediate.</param>
    /// <returns>A completed task.</returns>
    public Task ScrollToTimeAsync(TimeSpan timeOfDay, bool animated = false)
    {
        _ = animated;
        var timeScale = BuildTimeScale();
        double offset = timeScale.YForTime(timeOfDay) - timeScale.TopPadding;
        scrollY = ClampVerticalToView(offset);
        PublishVerticalOffset();
        Invalidate();
        return Task.CompletedTask;
    }

    /// <summary>Re-reads <see cref="ItemsSource"/> and repaints — call after mutating item times in place.</summary>
    public void RefreshItems() => OnDataChanged();

    /// <summary>
    /// Hit-tests an appointment at a view point (logical points) and returns it with its rect in
    /// view coordinates, for the native quick-action menu's preview lift.
    /// </summary>
    /// <param name="viewPoint">Point in the canvas's view coordinates.</param>
    /// <returns>The appointment and its view-space rect, or null when none is under the point.</returns>
    internal (IScheduleItem Item, RectF Rect)? HitTestMenuItem(PointF viewPoint)
    {
        float localX = viewPoint.X - RailWidth + (float)horizontalOffset - (stripStartDay * dayWidth);
        float localY = viewPoint.Y - BodyTop + (float)scrollY;
        float offsetX = RailWidth - (float)horizontalOffset + (stripStartDay * dayWidth);
        float offsetY = BodyTop - (float)scrollY;

        foreach (var (item, rect) in hitMap)
        {
            if (rect.Contains(localX, localY))
            {
                return (item, new RectF(rect.Left + offsetX, rect.Top + offsetY, rect.Width, rect.Height));
            }
        }

        return null;
    }

    /// <summary>Returns the quick-action menu items for an appointment (empty when no provider).</summary>
    internal IReadOnlyList<ScheduleMenuAction> GetItemActions(IScheduleItem item)
        => ItemActionsProvider?.Invoke(item) ?? Array.Empty<ScheduleMenuAction>();

    /// <summary>Raises <see cref="ItemActionInvoked"/> for a chosen menu action.</summary>
    internal void RaiseItemAction(IScheduleItem item, string label)
        => ItemActionInvoked?.Invoke(this, new ScheduleItemActionEventArgs(item, label));

    private static double ClampVertical(double offset, float bodyHeight, TimeScale timeScale)
    {
        double max = Math.Max(0, timeScale.TotalHeight - bodyHeight);
        return Math.Clamp(offset, 0, max);
    }

    private static SKColor ToSk(Color? color)
    {
        var c = color ?? Colors.Transparent;
        return new SKColor(
            (byte)Math.Round(c.Red * 255),
            (byte)Math.Round(c.Green * 255),
            (byte)Math.Round(c.Blue * 255),
            (byte)Math.Round(c.Alpha * 255));
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // Ease-out-cubic: fast start, gentle settle, never exceeds 1 (so it always fits the column).
    private static float EaseOutCubic(float t)
    {
        float x = 1f - t;
        return 1f - (x * x * x);
    }

    // Keeps [start, start+duration] within its day.
    private static DateTime ClampToDay(DateTime start, DateTime day, TimeSpan duration)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        if (start < dayStart)
        {
            start = dayStart;
        }

        if (start + duration > dayEnd)
        {
            start = dayEnd - duration;
        }

        return start < dayStart ? dayStart : start;
    }

    private TimeScale BuildTimeScale()
        => new TimeScale((float)EffectiveHourHeight, (float)TopContentInset, (float)BottomContentInset);

#if IOS
    private void AttachIosMenu()
    {
        if (iosMenu is null && canvas.Handler?.PlatformView is UIKit.UIView view)
        {
            iosMenu = QuickActionMenu.AttachIosInfinite(view, this);
        }
    }
#endif

    private void MarkStripDirty()
    {
        stripDirty = true;
        Invalidate();
    }

    // Clears all in-flight touch/gesture state — used when a native menu consumes the gesture and no
    // touch-up will arrive, so a lingering touch can't be misread as a pinch on the next drag.
    private void ResetTouchState()
    {
        touches.Clear();
        interaction = InteractionKind.None;
        inPinch = false;
        panning = false;
        lockAxis = 0;
        pressValid = false;
        StopMotion();
    }

    // Fires VisibleRangeChanged + updates CurrentDay when the leftmost visible day changes.
    private void ReportVisibleRange(int firstVisibleDay)
    {
        if (reportedFirstDay == firstVisibleDay)
        {
            return;
        }

        reportedFirstDay = firstVisibleDay;
        var firstDate = AnchorDay.Date.AddDays(firstVisibleDay);
        var lastDate = firstDate.AddDays(Math.Max(1, ViewMode) - 1);

        suppressCurrentDaySync = true;
        CurrentDay = firstDate;
        suppressCurrentDaySync = false;

        VisibleRangeChanged?.Invoke(this, new ScheduleVisibleRangeChangedEventArgs(firstDate, lastDate));
    }

    private void OnCurrentDayChanged(DateTime value)
    {
        if (suppressCurrentDaySync || dayWidth <= 0)
        {
            return;
        }

        int dayIndex = (int)(value.Date - AnchorDay.Date).TotalDays;
        horizontalOffset = ClampHorizontal(dayIndex * (double)dayWidth);
        Invalidate();
    }

    private void OnTypingItemChanged(object? oldValue, object? newValue)
    {
        // Pop the draft in when it first appears — the render loop drives the frames.
        if (oldValue is null && newValue is not null)
        {
            typingAppearStart = clock.Elapsed.TotalSeconds;
            BeginRenderLoop();
        }

        Invalidate();
    }

    // Typing pop-in progress, 0..1 (1 = done / not animating).
    private float OverlayAppearProgress()
    {
        if (double.IsNaN(typingAppearStart))
        {
            return 1f;
        }

        double t = (clock.Elapsed.TotalSeconds - typingAppearStart) / TypingAppearDuration;
        return t >= 1 ? 1f : (float)Math.Max(0, t);
    }

    private void OnDataChanged()
    {
        items = ItemsSource is null
            ? new List<IScheduleItem>()
            : ItemsSource.OfType<IScheduleItem>().ToList();
        RebuildItemIndex();
        stripDirty = true;
        Invalidate();
    }

    // Buckets each item under every day it overlaps, so RecordStrip only touches the visible window.
    private void RebuildItemIndex()
    {
        itemsByDay.Clear();
        foreach (var item in items)
        {
            for (var d = item.Start.Date; d <= item.End.Date; d = d.AddDays(1))
            {
                var key = DateOnly.FromDateTime(d);
                if (!itemsByDay.TryGetValue(key, out var list))
                {
                    list = new List<IScheduleItem>();
                    itemsByDay[key] = list;
                }

                list.Add(item);
            }
        }
    }

    private void OnLayoutChanged()
    {
        float body = (float)Width - RailWidth;
        dayWidth = body > 0 ? body / Math.Max(1, ViewMode) : 0;
        stripDirty = true;
        Invalidate();
    }

    private void Invalidate() => canvas.InvalidateSurface();

    private int? DayIndex(DateTime? day)
        => day is DateTime d ? (int)(d.Date - AnchorDay.Date).TotalDays : (int?)null;

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var skCanvas = e.Surface.Canvas;

        float logicalW = (float)canvas.Width;
        float logicalH = (float)canvas.Height;
        if (logicalW <= 0 || logicalH <= 0 || dayWidth <= 0)
        {
            skCanvas.Clear(ToSk(ActiveTheme.Background));
            return;
        }

        // Advance momentum for this frame (render loop is active while flinging).
        if (flinging)
        {
            StepFling();
        }

        // Draw in logical units; the GL surface is in pixels.
        float scale = e.BackendRenderTarget.Width / logicalW;
        touchScale = scale; // authoritative px→logical ratio for touch coords
        skCanvas.Scale(scale);

        var theme = ActiveTheme;
        var timeScale = BuildTimeScale();
        float railWidth = RailWidth;
        float headerHeight = HeaderHeight;
        float bodyWidth = logicalW - railWidth;

        // Record first: the strip re-layout is what sizes the all-day panel, and the panel eats
        // into the body's height.
        EnsureStrip(bodyWidth, timeScale);

        float bodyTop = BodyTop;
        float bodyHeight = logicalH - bodyTop;

        if (double.IsNaN(scrollY))
        {
            // Honor an explicit VerticalOffset; otherwise land around 07:00 on first paint.
            double seed = VerticalOffset > 0
                ? VerticalOffset
                : timeScale.YForTime(TimeSpan.FromHours(7)) - timeScale.TopPadding;
            scrollY = ClampVertical(seed, bodyHeight, timeScale);
        }

        skCanvas.Clear(ToSk(theme.Background));

        ReportVisibleRange(InfiniteScheduleGeometry.FirstVisibleDay(horizontalOffset, dayWidth));

        float stripTx = railWidth - (float)horizontalOffset + (stripStartDay * dayWidth);

        // Body: replay the recorded day buffer, translated by the owned offset and clipped below the header.
        // While pinching, scale it vertically (cheap) instead of re-recording at the new HourHeight.
        if (strip is not null)
        {
            skCanvas.Save();
            skCanvas.ClipRect(new SKRect(railWidth, bodyTop, logicalW, logicalH));
            float ty = bodyTop - (float)scrollY;
            skCanvas.Translate(stripTx, ty);

            float vScale = stripHourHeight > 0 ? (float)(EffectiveHourHeight / stripHourHeight) : 1f;
            if (Math.Abs(vScale - 1f) > 0.001f)
            {
                skCanvas.Scale(1f, vScale);
            }

            skCanvas.DrawPicture(strip);
            skCanvas.Restore();
        }

        overlayNeedsFrame = false;
        DrawOverlays(skCanvas, railWidth, bodyTop, logicalW, logicalH);
        DrawHourRail(skCanvas, timeScale, railWidth, bodyTop, logicalH);
        DrawNowMarker(skCanvas, timeScale, railWidth, bodyTop, logicalW, logicalH, bodyWidth);
        DrawAllDayBand(skCanvas, railWidth, headerHeight, logicalW, stripTx);
        DrawHeaderBand(skCanvas, railWidth, headerHeight, logicalW, bodyWidth);

        // The pop-in runs on the render loop; drop back to on-demand painting once it (and any
        // scroll/fling/pinch) is done, so the block always settles at full size.
        if (!overlayNeedsFrame && !flinging && !panning && !inPinch && canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = false;
        }
    }

    private void EnsureStrip(float bodyWidth, TimeScale timeScale)
    {
        int firstVisible = InfiniteScheduleGeometry.FirstVisibleDay(horizontalOffset, dayWidth);
        int visibleCount = InfiniteScheduleGeometry.VisibleDayCount(bodyWidth, dayWidth);

        bool needsRebuild = stripDirty
            || strip is null
            || firstVisible < stripStartDay + 1
            || firstVisible + visibleCount > stripStartDay + stripDayCount - 1;

        if (!needsRebuild)
        {
            return;
        }

        stripStartDay = firstVisible - BufferDays;
        stripDayCount = visibleCount + (2 * BufferDays);
        RecordStrip(timeScale);
        stripDirty = false;
    }

    private void RecordStrip(TimeScale timeScale)
    {
        var start = AnchorDay.Date.AddDays(stripStartDay);
        var end = start.AddDays(stripDayCount - 1);

        // Only the strip's days, deduped (a multi-day item sits in several buckets).
        var stripItems = new List<IScheduleItem>();
        var seen = new HashSet<IScheduleItem>();
        for (int d = 0; d < stripDayCount; d++)
        {
            var key = DateOnly.FromDateTime(start.AddDays(d));
            if (itemsByDay.TryGetValue(key, out var dayList))
            {
                foreach (var it in dayList)
                {
                    if (seen.Add(it))
                    {
                        stripItems.Add(it);
                    }
                }
            }
        }

        // All-day/multi-day items are excluded from the columns below, so they only exist in the panel.
        // Laid out over the whole strip (not just the visible window) to keep the panel height steady while panning.
        BuildAllDayBars(stripItems, DateOnly.FromDateTime(start));

        var columns = ScheduleColumnBuilder.Build(
            start,
            end,
            stripDayCount,
            Persons,
            stripItems,
            DateOnly.FromDateTime(DateTime.Today));

        // Columns come day-major then person, so a uniform column width tiles them left-to-right.
        float columnWidth = dayWidth / Math.Max(1, PersonCount);
        float stripWidth = stripDayCount * dayWidth;
        float contentHeight = timeScale.TotalHeight;
        stripHourHeight = timeScale.HourHeight;

        hitMap.Clear();

        using var recorder = new SKPictureRecorder();
        var rc = recorder.BeginRecording(new SKRect(0, 0, stripWidth, contentHeight));

        DrawGrid(rc, timeScale, columns.Length, columnWidth, contentHeight);
        for (int i = 0; i < columns.Length; i++)
        {
            DrawColumnItems(rc, columns[i], i * columnWidth, columnWidth, timeScale);
        }

        strip?.Dispose();
        strip = recorder.EndRecording();
    }

    private void BuildAllDayBars(List<IScheduleItem> stripItems, DateOnly rangeStart)
    {
        if (!ShowAllDay)
        {
            allDayBars = Array.Empty<AllDayBar>();
            allDayHeight = 0f;
            return;
        }

        var spanning = new List<IScheduleItem>();
        foreach (var item in stripItems)
        {
            if (AllDayLayout.IsSpanning(item))
            {
                spanning.Add(item);
            }
        }

        allDayBars = AllDayLayout.Layout(spanning, rangeStart, stripDayCount);

        int lanes = 0;
        foreach (var bar in allDayBars)
        {
            lanes = Math.Max(lanes, bar.Lane + 1);
        }

        allDayHeight = lanes > 0 ? (lanes * AllDayLaneHeight) + AllDayPanelPadding : 0f;
    }

    // The all-day panel between the header and the grid. Painted live (not into the strip picture)
    // so it can pin to the viewport's top while the body scrolls vertically underneath it.
    private void DrawAllDayBand(SKCanvas skCanvas, float railWidth, float top, float logicalW, float tx)
    {
        allDayHitMap.Clear();
        if (allDayHeight <= 0)
        {
            return;
        }

        var theme = ActiveTheme;
        var renderer = ActiveRenderer;
        var stripStart = DateOnly.FromDateTime(AnchorDay.Date.AddDays(stripStartDay));
        float bottom = top + allDayHeight;

        using (var bg = new SKPaint { Color = ToSk(theme.Background), Style = SKPaintStyle.Fill })
        {
            skCanvas.DrawRect(new SKRect(0, top, logicalW, bottom), bg);
        }

        using (var line = new SKPaint { Color = ToSk(theme.GridLine), StrokeWidth = 1, IsAntialias = false })
        {
            skCanvas.DrawLine(0, bottom - 0.5f, logicalW, bottom - 0.5f, line);
        }

        skCanvas.Save();
        skCanvas.ClipRect(new SKRect(railWidth, top, logicalW, bottom));

        foreach (var bar in allDayBars)
        {
            float left = tx + (bar.StartDay * dayWidth) + 2f;
            float right = tx + ((bar.EndDay + 1) * dayWidth) - 2f;
            if (right < railWidth || left > logicalW)
            {
                continue;
            }

            float y = top + (bar.Lane * AllDayLaneHeight) + 2f;
            var rect = new SKRect(left, y, right, y + AllDayLaneHeight - 4f);

            renderer.DrawAllDayBar(skCanvas, new InfiniteAllDayBlock
            {
                Rect = rect,
                Color = ToSk(bar.Item.Color ?? theme.Accent),
                Theme = theme,
                Title = bar.Item.Title,
                ContinuesLeft = DateOnly.FromDateTime(bar.Item.Start) < stripStart.AddDays(bar.StartDay),
                ContinuesRight = AllDayLayout.EndDateOf(bar.Item) > stripStart.AddDays(bar.EndDay),
                Item = bar.Item,
            });

            allDayHitMap.Add((bar.Item, rect));
        }

        skCanvas.Restore();

        // Rail label, drawn after the clip so it can sit left of the day columns.
        using var text = new SKPaint { Color = ToSk(theme.Muted), IsAntialias = true };
        using var font = new SKFont { Size = (float)theme.HourLabelFontSize };
        skCanvas.DrawText("all-day", railWidth - 6, top + AllDayLaneHeight - 5f, SKTextAlign.Right, font, text);
    }

    private void DrawGrid(SKCanvas rc, TimeScale timeScale, int columnCount, float columnWidth, float height)
    {
        var theme = ActiveTheme;
        float width = columnCount * columnWidth;
        using var line = new SKPaint { Color = ToSk(theme.GridLine), StrokeWidth = 1, IsAntialias = false };

        for (int h = 0; h <= 24; h++)
        {
            float y = timeScale.YForTime(TimeSpan.FromHours(h));
            rc.DrawLine(0, y, width, y, line);
        }

        for (int c = 0; c <= columnCount; c++)
        {
            float x = c * columnWidth;
            rc.DrawLine(x, 0, x, height, line);
        }
    }

    private void DrawColumnItems(SKCanvas rc, ScheduleViewColumn column, float columnX, float columnWidth, TimeScale timeScale)
    {
        var theme = ActiveTheme;
        var renderer = ActiveRenderer;
        var dayStart = column.DayStart;
        var dayEnd = dayStart.AddDays(1);

        foreach (var laid in column.Items)
        {
            var item = laid.Item;
            var clipStart = item.Start < dayStart ? dayStart : item.Start;
            var clipEnd = item.End > dayEnd ? dayEnd : item.End;

            float slotW = columnWidth / Math.Max(1, laid.ColumnsInGroup);
            float x = columnX + (laid.Column * slotW) + 2;
            float y1 = timeScale.YForTime(clipStart - dayStart);
            float y2 = timeScale.YForTime(clipEnd - dayStart);
            float rw = slotW - 4;
            float rh = MathF.Max(y2 - y1, 20);
            var rect = new SKRect(x, y1, x + rw, y1 + rh);

            hitMap.Add((item, rect));

            renderer.DrawAppointment(rc, new InfiniteScheduleBlock
            {
                Rect = rect,
                Color = ToSk(item.Color ?? column.Accent ?? theme.Accent),
                Theme = theme,
                Title = item.Title,
                Start = item.Start,
                End = item.End,
                Item = item,
            });
        }
    }

    private void DrawHourRail(SKCanvas skCanvas, TimeScale timeScale, float railWidth, float headerHeight, float logicalH)
    {
        var theme = ActiveTheme;
        skCanvas.Save();
        skCanvas.ClipRect(new SKRect(0, headerHeight, railWidth, logicalH));

        using var bg = new SKPaint { Color = ToSk(theme.Background), Style = SKPaintStyle.Fill };
        skCanvas.DrawRect(new SKRect(0, headerHeight, railWidth, logicalH), bg);

        using var text = new SKPaint { Color = ToSk(theme.Muted), IsAntialias = true };
        using var font = new SKFont { Size = (float)theme.HourLabelFontSize };

        for (int h = 0; h <= 24; h++)
        {
            float y = headerHeight - (float)scrollY + timeScale.YForTime(TimeSpan.FromHours(h));
            if (y < headerHeight - 8 || y > logicalH + 8)
            {
                continue;
            }

            skCanvas.DrawText($"{h:00}:00", railWidth - 6, y + 4, SKTextAlign.Right, font, text);
        }

        skCanvas.Restore();
    }

    // Current-time marker: a NowIndicator-coloured line across today's column at the current time,
    // plus a time badge in the rail — only when today is one of the visible days. Drawn live
    // (not into the cached day strip) so it advances with the minute timer.
    private void DrawNowMarker(SKCanvas skCanvas, TimeScale timeScale, float railWidth, float headerHeight, float logicalW, float logicalH, float bodyWidth)
    {
        var now = DateTime.Now;
        int todayIndex = (now.Date - AnchorDay.Date).Days;
        int firstVisible = InfiniteScheduleGeometry.FirstVisibleDay(horizontalOffset, dayWidth);
        int visibleCount = InfiniteScheduleGeometry.VisibleDayCount(bodyWidth, dayWidth);
        if (todayIndex < firstVisible || todayIndex >= firstVisible + visibleCount)
        {
            return;
        }

        float y = headerHeight - (float)scrollY + timeScale.YForTime(now.TimeOfDay);
        if (y < headerHeight - 1 || y > logicalH + 1)
        {
            return;
        }

        var color = ToSk(ActiveTheme.NowIndicator);

        // Line spans only TODAY's column — in multi-day view the other visible columns aren't "now",
        // so it must not bleed across them. Clip to today's column intersected with the visible body.
        float todayLeft = railWidth - (float)horizontalOffset + (todayIndex * dayWidth);
        float lineLeft = Math.Max(railWidth, todayLeft);
        float lineRight = Math.Min(logicalW, todayLeft + dayWidth);
        if (lineRight > lineLeft)
        {
            skCanvas.Save();
            skCanvas.ClipRect(new SKRect(lineLeft, headerHeight, lineRight, logicalH));
            using var line = new SKPaint { Color = color, StrokeWidth = 1.5f, IsAntialias = true };
            skCanvas.DrawLine(lineLeft, y, lineRight, y, line);
            skCanvas.Restore();
        }

        // Time badge in the rail.
        float fontSize = (float)ActiveTheme.HourLabelFontSize;
        float badgeH = fontSize + 8f;
        float badgeW = railWidth - 6f;
        float badgeX = 2f;
        float badgeY = y - (badgeH / 2f);

        skCanvas.Save();
        skCanvas.ClipRect(new SKRect(0, headerHeight, railWidth, logicalH));
        using (var badge = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill })
        using (var rr = new SKRoundRect(new SKRect(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH), badgeH / 2f))
        {
            skCanvas.DrawRoundRect(rr, badge);
        }

        using (var txt = new SKPaint { Color = SKColors.White, IsAntialias = true })
        using (var font = new SKFont { Size = fontSize })
        {
            var metrics = font.Metrics;
            float baseline = (badgeY + (badgeH / 2f)) - ((metrics.Ascent + metrics.Descent) / 2f);
            skCanvas.DrawText(now.ToString("HH:mm", CultureInfo.CurrentCulture), badgeX + (badgeW / 2f), baseline, SKTextAlign.Center, font, txt);
        }

        skCanvas.Restore();
    }

    private void StartNowTimer()
    {
        if (nowTimer is not null)
        {
            return;
        }

        nowTimer = Dispatcher.CreateTimer();
        nowTimer.Interval = TimeSpan.FromSeconds(30);
        nowTimer.Tick += (_, _) => canvas.InvalidateSurface();
        nowTimer.Start();
    }

    private void StopNowTimer()
    {
        nowTimer?.Stop();
        nowTimer = null;
    }

    // Whole header band (blank rail corner + day/person row), delegated to the active renderer so
    // callers can restyle it. Builds the visible-day layout once and hands it over; the default
    // reproduces the built-in look.
    private void DrawHeaderBand(SKCanvas skCanvas, float railWidth, float headerHeight, float logicalW, float bodyWidth)
    {
        var persons = Persons is { Count: > 0 } p
            ? (p as IReadOnlyList<IPerson> ?? new List<IPerson>(p))
            : null;
        int personCount = persons?.Count ?? 1;

        int firstVisible = InfiniteScheduleGeometry.FirstVisibleDay(horizontalOffset, dayWidth);
        int visibleCount = InfiniteScheduleGeometry.VisibleDayCount(bodyWidth, dayWidth);
        var todayDate = DateTime.Today;

        var days = new List<InfiniteScheduleHeaderDay>(Math.Max(0, visibleCount));
        for (int i = firstVisible; i < firstVisible + visibleCount; i++)
        {
            var day = AnchorDay.Date.AddDays(i);
            days.Add(new InfiniteScheduleHeaderDay
            {
                Day = day,
                IsToday = day == todayDate,
                Left = railWidth - (float)horizontalOffset + (i * dayWidth),
                Width = dayWidth,
            });
        }

        var ctx = new InfiniteScheduleHeaderContext
        {
            Theme = ActiveTheme,
            RailWidth = railWidth,
            HeaderHeight = headerHeight,
            Width = logicalW,
            Persons = persons,
            PersonColumnWidth = dayWidth / personCount,
            Days = days,
        };

        skCanvas.Save();
        skCanvas.ClipRect(new SKRect(0, 0, logicalW, headerHeight));
        ActiveRenderer.DrawHeader(skCanvas, ctx);
        skCanvas.Restore();
    }

    // Live overlays drawn on top of the cached strip: the typing draft and the holding ghost.
    private void DrawOverlays(SKCanvas skCanvas, float railWidth, float headerHeight, float logicalW, float logicalH)
    {
        if (TypingItem is null && HoldingSchedule is null)
        {
            return;
        }

        var theme = ActiveTheme;
        var renderer = ActiveRenderer;
        skCanvas.Save();
        skCanvas.ClipRect(new SKRect(railWidth, headerHeight, logicalW, logicalH));

        if (TypingItem is { } typing && TypingScreenRect() is SKRect tr)
        {
            var typingBlock = new InfiniteScheduleBlock
            {
                Rect = tr,
                Color = ToSk(typing.Color ?? theme.Accent),
                Theme = theme,
                Title = typing.Title,
                Start = typing.Start,
                End = typing.End,
            };

            float progress = OverlayAppearProgress();
            if (progress < 1f)
            {
                // Pop-in: scale about the block center (a cheap transform — no offscreen layer).
                overlayNeedsFrame = true;
                float s = 0.6f + (0.4f * EaseOutCubic(progress));
                skCanvas.Save();
                skCanvas.Scale(s, s, tr.MidX, tr.MidY);
                renderer.DrawTypingItem(skCanvas, typingBlock);
                skCanvas.Restore();
            }
            else
            {
                renderer.DrawTypingItem(skCanvas, typingBlock);
            }
        }

        if (HoldingSchedule is { } holding && HoldingScreenRect() is SKRect hr)
        {
            // While dragging, tie the ghost back to its original slot so it's clear what's moving.
            if (holdingActive)
            {
                var origin = BlockScreenRect(holding.Start, holding.End, holding.PersonId);
                if (Math.Abs(origin.MidX - hr.MidX) > 1 || Math.Abs(origin.MidY - hr.MidY) > 1)
                {
                    var link = ToSk(holding.Color ?? theme.Accent);
                    using var originStroke = new SKPaint { Color = link.WithAlpha(120), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
                    skCanvas.DrawRoundRect(origin, 6, 6, originStroke);

                    using var lineDash = SKPathEffect.CreateDash(new float[] { 5, 4 }, 0);
                    using var connector = new SKPaint { Color = link, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = lineDash };
                    skCanvas.DrawLine(origin.MidX, origin.MidY, hr.MidX, hr.MidY, connector);

                    using var anchor = new SKPaint { Color = link, IsAntialias = true, Style = SKPaintStyle.Fill };
                    skCanvas.DrawCircle(origin.MidX, origin.MidY, 4, anchor);
                }
            }

            renderer.DrawHoldingItem(skCanvas, new InfiniteScheduleBlock
            {
                Rect = hr,
                Color = ToSk(holding.Color ?? theme.Accent),
                Theme = theme,
                Title = holding.Title,
                Start = holdingActive ? holdStart : holding.Start,
                End = holdingActive ? holdEnd : holding.End,
                IsDragging = holdingActive,
                Item = holding,
            });
        }

        skCanvas.Restore();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;
        var p = LogicalPoint(e.Location);
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                touches[e.Id] = p;
                if (touches.Count >= 2 && !inPinch)
                {
                    BeginPinch();
                }
                else if (!inPinch)
                {
                    OnTouchPressed(p);
                }

                break;

            case SKTouchAction.Moved:
                if (touches.ContainsKey(e.Id))
                {
                    touches[e.Id] = p;
                }

                if (inPinch)
                {
                    UpdatePinch();
                }
                else
                {
                    OnTouchMoved(p);
                }

                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                touches.Remove(e.Id);
                if (inPinch)
                {
                    // A pinch owns the whole gesture: single-finger scroll stays disabled until all lift.
                    if (touches.Count == 0)
                    {
                        EndPinch();
                    }
                }
                else
                {
                    OnTouchReleased(e.ActionType != SKTouchAction.Released);
                }

                break;
        }
    }

    // Pinch zooms the time axis (HourHeight), keeping the time under the pinch center fixed.
    private void BeginPinch()
    {
        if (!TryGetTwoTouches(out var a, out var b))
        {
            return;
        }

        // Abandon any single-finger interaction the first touch started.
        StopMotion();
        longPressTimer?.Stop();
        interaction = InteractionKind.None;
        pressValid = false;
        lockAxis = 0;
        inPinch = true;

        pinchStartDist = Math.Max(1.0, Distance(a, b));
        pinchStartHourHeight = HourHeight;
        liveHourHeight = HourHeight;

        double midY = (a.Y + b.Y) / 2.0;
        var timeScale = BuildTimeScale();
        double contentY = scrollY + (midY - BodyTop);
        pinchAnchorHours = (contentY - timeScale.TopPadding) / Math.Max(1.0, HourHeight);
        BeginRenderLoop();
    }

    private void UpdatePinch()
    {
        if (!TryGetTwoTouches(out var a, out var b))
        {
            return;
        }

        double scale = Distance(a, b) / pinchStartDist;
        double newHourHeight = Math.Clamp(pinchStartHourHeight * scale, 24.0, 300.0);
        double midY = (a.Y + b.Y) / 2.0;

        // Zoom live (no strip rebuild): the paint scales the cached picture; commit at pinch end.
        liveHourHeight = newHourHeight;

        var timeScale = BuildTimeScale();
        double contentY = timeScale.TopPadding + (pinchAnchorHours * newHourHeight);
        scrollY = ClampVerticalToView(contentY - (midY - BodyTop));
        Invalidate();
    }

    private void EndPinch()
    {
        inPinch = false;
        interaction = InteractionKind.None;
        pressValid = false;
        lockAxis = 0;
        panning = false;

        // Commit the zoom once — this re-records the strip at the final HourHeight.
        double finalHourHeight = double.IsNaN(liveHourHeight) ? HourHeight : liveHourHeight;
        liveHourHeight = double.NaN;
        HourHeight = finalHourHeight;

        StopMotion();
        Invalidate();
    }

    private bool TryGetTwoTouches(out Point a, out Point b)
    {
        a = default;
        b = default;
        if (touches.Count < 2)
        {
            return false;
        }

        int i = 0;
        foreach (var pt in touches.Values)
        {
            if (i == 0)
            {
                a = pt;
            }
            else
            {
                b = pt;
                return true;
            }

            i++;
        }

        return false;
    }

    private Point LogicalPoint(SKPoint px)
    {
        // SKGLView touch Location is in device pixels; convert with the same ratio the paint uses.
        // Fall back to CanvasSize before the first paint has set touchScale.
        double sc = touchScale > 0
            ? touchScale
            : (canvas.Width > 0 ? canvas.CanvasSize.Width / canvas.Width : 1.0);
        if (sc <= 0)
        {
            sc = 1.0;
        }

        return new Point(px.X / sc, px.Y / sc);
    }

    private void OnTouchPressed(Point p)
    {
        // Pressing while momentum is running "catches" it: stop, but remember so the release
        // settles to a day column and doesn't also fire a tap action (like a native list).
        caughtMotion = flinging;
        StopMotion();
        pressPoint = p;
        pressValid = p.X >= RailWidth && p.Y >= HeaderHeight;
        longFired = false;
        lockAxis = 0;
        velocityX = 0;
        velocityY = 0;
        panning = true;
        panStartHorizontal = horizontalOffset;
        panStartVertical = scrollY;
        lastPanTotalX = 0;
        lastPanTotalY = 0;
        panSampleSeconds = clock.Elapsed.TotalSeconds;

        // Grabbing the holding ghost or the typing draft starts a block drag instead of a scroll.
        if (pressValid && HoldingSchedule is not null && HoldingScreenRect() is SKRect hr
            && new SKRect(hr.Left - HoldingHalo, hr.Top - HoldingHalo, hr.Right + HoldingHalo, hr.Bottom + HoldingHalo)
                .Contains((float)p.X, (float)p.Y))
        {
            BeginBlockDrag(InteractionKind.Holding, p, hr);
            return;
        }

        if (pressValid && TypingItem is not null && TypingScreenRect() is SKRect tr
            && tr.Contains((float)p.X, (float)p.Y))
        {
            BeginBlockDrag(InteractionKind.Typing, p, tr);
            return;
        }

        interaction = InteractionKind.Scroll;
        if (pressValid)
        {
            longPressTimer ??= CreateLongPressTimer();
            longPressTimer.Stop();
            longPressTimer.Start();
        }
    }

    private void OnTouchMoved(Point p)
    {
        if (interaction == InteractionKind.Holding)
        {
            UpdateBlockDrag(p, holding: true);
            return;
        }

        if (interaction == InteractionKind.Typing)
        {
            UpdateBlockDrag(p, holding: false);
            return;
        }

        double totalX = p.X - pressPoint.X;
        double totalY = p.Y - pressPoint.Y;

        // Commit to the dominant axis once the finger clearly moves (no diagonal wobble).
        if (lockAxis == 0)
        {
            if (Math.Abs(totalX) < PanLockThreshold && Math.Abs(totalY) < PanLockThreshold)
            {
                return;
            }

            lockAxis = Math.Abs(totalX) >= Math.Abs(totalY) ? 1 : 2;
            pressValid = false;
            longPressTimer?.Stop();
            BeginRenderLoop();
        }

        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Max(now - panSampleSeconds, 1e-3);

        // EMA keeps the flick's peak velocity (the last pre-lift sample is usually decelerating).
        if (lockAxis == 1)
        {
            double instX = (totalX - lastPanTotalX) / dt;
            velocityX = (velocityX * (1 - VelocitySmoothing)) + (instX * VelocitySmoothing);
            velocityY = 0;
            horizontalOffset = ClampHorizontal(panStartHorizontal - totalX);
        }
        else
        {
            double instY = (totalY - lastPanTotalY) / dt;
            velocityY = (velocityY * (1 - VelocitySmoothing)) + (instY * VelocitySmoothing);
            velocityX = 0;
            scrollY = ClampVerticalToView(panStartVertical - totalY);
        }

        lastPanTotalX = totalX;
        lastPanTotalY = totalY;
        panSampleSeconds = now;
        Invalidate();
    }

    private void OnTouchReleased(bool cancelled)
    {
        panning = false;

        if (interaction == InteractionKind.Holding)
        {
            EndHoldingDrag(drop: !cancelled);
            interaction = InteractionKind.None;
            StopMotion();
            return;
        }

        if (interaction == InteractionKind.Typing)
        {
            interaction = InteractionKind.None;
            StopMotion();
            return;
        }

        longPressTimer?.Stop();

        if (lockAxis == 1 && PagingEnabled && Math.Abs(velocityX) >= PageFlickSpeed)
        {
            // Horizontal flick → advance exactly one chunk (a screenful) in the flick direction.
            StartChunkFlick();
        }
        else if (lockAxis == 1 && PagingEnabled)
        {
            // Slow horizontal drag → snap to the nearest day.
            StartHorizontalPaging();
        }
        else if (lockAxis != 0)
        {
            // Vertical (or paging off) → free momentum, settles on a day.
            StartFling();
        }
        else
        {
            // A plain tap fires an action; a tap that caught a fling only stops it. Either way,
            // settle the day axis to the nearest column so it never rests mid-day.
            if (pressValid && !longFired && !caughtMotion)
            {
                DispatchTap(pressPoint, longPress: false);
            }

            SettleToDay();
        }

        caughtMotion = false;
        interaction = InteractionKind.None;
    }

    // Flick paging: advance exactly one chunk (a screenful = ViewMode days) from the gesture's start
    // day, in the flick direction, then ease onto that day. Repeated flicks step chunk by chunk.
    private void StartChunkFlick()
    {
        double snapWidth = dayWidth;
        if (snapWidth <= 0)
        {
            StopMotion();
            return;
        }

        int startDay = (int)Math.Round(panStartHorizontal / snapWidth);
        int step = Math.Max(1, ViewMode);
        int target = velocityX < 0 ? startDay + step : startDay - step;
        BeginSettle(ClampHorizontal(target * snapWidth));
    }

    // Slow-drag day snap: settle the leftmost column to the nearest whole day (drag past half a day
    // → next day, less → snaps back). Flicks don't come here — they run the momentum fling instead.
    private void StartHorizontalPaging()
    {
        double snapWidth = dayWidth;
        if (snapWidth <= 0)
        {
            StopMotion();
            return;
        }

        int target = (int)Math.Round(horizontalOffset / snapWidth); // nearest day
        BeginSettle(ClampHorizontal(target * snapWidth));
    }

    // Starts the spring settle toward <paramref name="target"/>, carrying the finger's release velocity
    // (content moves opposite the finger) so the animation continues the gesture rather than restarting.
    // The seed is capped at the no-overshoot limit for a critically damped spring, |x| / SettleTau.
    private void BeginSettle(double target)
    {
        double distance = target - horizontalOffset;
        double seed = Math.Clamp(-velocityX, -MaxFlingSpeed, MaxFlingSpeed);
        double limit = Math.Abs(distance) / SettleTau;

        // Only momentum heading toward the target is useful; a backwards flick would fight the spring.
        velocityX = seed * distance > 0 ? Math.Clamp(seed, -limit, limit) : 0;
        velocityY = 0;
        settleTargetH = target;
        flinging = true;
        settling = true;
        lastFrameSeconds = clock.Elapsed.TotalSeconds;
        BeginRenderLoop();
    }

    // Eases the horizontal offset onto the nearest whole day, then goes idle. No-op (just stops)
    // when already aligned.
    private void SettleToDay()
    {
        double target = ClampHorizontal(InfiniteScheduleGeometry.SnapToDay(horizontalOffset, dayWidth));
        if (Math.Abs(target - horizontalOffset) < 0.5)
        {
            horizontalOffset = target;
            StopMotion();
            Invalidate();
            return;
        }

        BeginSettle(target);
    }

    private void BeginBlockDrag(InteractionKind kind, Point p, SKRect rect)
    {
        interaction = kind;
        pressValid = false;
        longFired = false;
        longPressTimer?.Stop();

        if (kind == InteractionKind.Holding)
        {
            var it = HoldingSchedule;
            if (it is null)
            {
                interaction = InteractionKind.None;
                return;
            }

            dragOriginStart = it.Start;
            dragOriginEnd = it.End;
            dragOriginPersonId = it.PersonId;
            holdStart = it.Start;
            holdEnd = it.End;
            holdPersonId = it.PersonId;
            holdingActive = true;
        }
        else
        {
            var it = TypingItem;
            if (it is null)
            {
                interaction = InteractionKind.None;
                return;
            }

            dragOriginStart = it.Start;
            dragOriginEnd = it.End;
        }

        // Resize bands run the FULL WIDTH along the top (start) and bottom (end) edges; the middle
        // moves. A block's time is vertical, so the top/bottom edge is the natural grab — corners
        // would force pixel-accurate diagonal aiming. Band height is capped so short blocks still
        // keep a middle "move" zone (top third / bottom third at most).
        float edge = Math.Min(18f, rect.Height / 3f);
        float relY = (float)p.Y - rect.Top;
        bool inTop = relY < edge;
        bool inBottom = (rect.Height - relY) < edge;
        blockMode = inTop ? BlockDragMode.ResizeStart
            : inBottom ? BlockDragMode.ResizeEnd
            : BlockDragMode.Move;

        dragStartPoint = p;
        BeginRenderLoop();
        Invalidate();
    }

    private void UpdateBlockDrag(Point p, bool holding)
    {
        double dy = p.Y - dragStartPoint.Y;
        double snapped = Math.Round(dy / HourHeight * 60.0 / SnapMinutes) * SnapMinutes;
        var duration = dragOriginEnd - dragOriginStart;
        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(SnapMinutes * 2);
        }

        DateTime newStart = dragOriginStart;
        DateTime newEnd = dragOriginEnd;
        string? personId = holding ? holdPersonId : TypingItem?.PersonId;

        switch (blockMode)
        {
            case BlockDragMode.Move:
            {
                var day = HitTestColumnDay((float)p.X, out string? pid);
                var tentative = day.Add(dragOriginStart.TimeOfDay).AddMinutes(snapped);
                var clamped = ClampToDay(tentative, day, duration);
                newStart = clamped;
                newEnd = clamped + duration;
                if (pid is not null)
                {
                    personId = pid;
                }

                break;
            }

            case BlockDragMode.ResizeStart:
            {
                var s = dragOriginStart.AddMinutes(snapped);
                var dayStart = dragOriginStart.Date;
                if (s < dayStart)
                {
                    s = dayStart;
                }

                if (s > dragOriginEnd.AddMinutes(-SnapMinutes))
                {
                    s = dragOriginEnd.AddMinutes(-SnapMinutes);
                }

                newStart = s;
                break;
            }

            case BlockDragMode.ResizeEnd:
            {
                var en = dragOriginEnd.AddMinutes(snapped);
                var minEnd = dragOriginStart.AddMinutes(SnapMinutes);
                if (en < minEnd)
                {
                    en = minEnd;
                }

                var dayEnd = dragOriginStart.Date.AddDays(1);
                if (en > dayEnd)
                {
                    en = dayEnd;
                }

                newEnd = en;
                break;
            }
        }

        if (holding)
        {
            holdStart = newStart;
            holdEnd = newEnd;
            holdPersonId = personId;
        }
        else if (TypingItem is ITypingScheduleItem typing)
        {
            typing.Start = newStart;
            typing.End = newEnd;
            if (personId is not null)
            {
                typing.PersonId = personId;
            }
        }

        Invalidate();
    }

    private void EndHoldingDrag(bool drop)
    {
        var item = HoldingSchedule;

        // A release with no movement is just a pick-up (e.g. long-press then lift): keep the block
        // parked so the user can grab and drag it later, rather than firing a drop that clears it.
        bool moved = holdStart != dragOriginStart
            || holdEnd != dragOriginEnd
            || !string.Equals(holdPersonId, dragOriginPersonId, StringComparison.Ordinal);

        if (drop && moved && item is not null)
        {
            HoldingDropped?.Invoke(this, new HoldingDroppedEventArgs(item, holdStart, holdEnd, holdPersonId));
        }

        // Event-only: the block returns to its natural position (the app decides whether to apply).
        holdingActive = false;
        Invalidate();
    }

    // Maps a screen X to the day (and person, when split) under it.
    private DateTime HitTestColumnDay(float screenX, out string? personId)
    {
        double absoluteX = horizontalOffset + (screenX - RailWidth);
        int dayIndex = (int)Math.Floor(absoluteX / dayWidth);
        var day = AnchorDay.Date.AddDays(dayIndex);

        personId = null;
        if (Persons is { Count: > 0 } persons)
        {
            float columnWidth = dayWidth / persons.Count;
            int slot = (int)Math.Floor((absoluteX - (dayIndex * dayWidth)) / columnWidth);
            personId = persons[Math.Clamp(slot, 0, persons.Count - 1)].Id;
        }

        return day;
    }

    private SKRect BlockScreenRect(DateTime start, DateTime end, string? personId)
    {
        var timeScale = BuildTimeScale();
        var day = start.Date;
        int dayIndex = (int)(day - AnchorDay.Date).TotalDays;
        float columnWidth = dayWidth / Math.Max(1, PersonCount);

        int slot = 0;
        if (Persons is { Count: > 0 } persons && personId is not null)
        {
            for (int i = 0; i < persons.Count; i++)
            {
                if (string.Equals(persons[i].Id, personId, StringComparison.Ordinal))
                {
                    slot = i;
                    break;
                }
            }
        }

        float contentX = (dayIndex * dayWidth) + (slot * columnWidth);
        float screenX = RailWidth - (float)horizontalOffset + contentX;

        var dayEnd = day.AddDays(1);
        var clipStart = start < day ? day : start;
        var clipEnd = end > dayEnd ? dayEnd : end;
        float top = (float)(BodyTop - scrollY) + timeScale.YForTime(clipStart - day);
        float bottom = (float)(BodyTop - scrollY) + timeScale.YForTime(clipEnd - day);
        float w = (PersonCount > 1 ? columnWidth : dayWidth) - 4;

        return new SKRect(screenX + 2, top, screenX + 2 + w, Math.Max(bottom, top + 20));
    }

    private SKRect? TypingScreenRect()
        => TypingItem is null ? null : BlockScreenRect(TypingItem.Start, TypingItem.End, TypingItem.PersonId);

    private SKRect? HoldingScreenRect()
    {
        var item = HoldingSchedule;
        if (item is null)
        {
            return null;
        }

        return holdingActive
            ? BlockScreenRect(holdStart, holdEnd, holdPersonId)
            : BlockScreenRect(item.Start, item.End, item.PersonId);
    }

    private void StartFling()
    {
        // Content moves opposite the finger; gain punches up the flick, then clamp the extremes.
        velocityX = Math.Clamp(-velocityX * FlingGain, -MaxFlingSpeed, MaxFlingSpeed);
        velocityY = Math.Clamp(-velocityY * FlingGain, -MaxFlingSpeed, MaxFlingSpeed);

        flinging = true;
        settling = false;
        lastFrameSeconds = clock.Elapsed.TotalSeconds;
        BeginRenderLoop();
    }

    // Advances momentum once per rendered frame (driven from OnPaintSurface), so motion is
    // vsync-paced rather than tied to throttled gesture/timer callbacks.
    private void StepFling()
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - lastFrameSeconds, 1e-3, 0.05);
        lastFrameSeconds = now;

        if (!settling)
        {
            horizontalOffset = ClampHorizontal(horizontalOffset + (velocityX * dt));
            scrollY = ClampVerticalToView(scrollY + (velocityY * dt));

            // Frame-rate-independent exponential decay.
            double decay = Math.Exp(-dt / FlingDecelerationTime);
            velocityX *= decay;
            velocityY *= decay;

            if (Math.Abs(velocityX) < FlingStopSpeed && Math.Abs(velocityY) < FlingStopSpeed)
            {
                settling = true;
                settleTargetH = ClampHorizontal(InfiniteScheduleGeometry.SnapToDay(horizontalOffset, dayWidth));
            }
        }
        else
        {
            // Critically damped spring, exact analytic step: keeps the finger's velocity flowing into
            // the page transition instead of restarting from zero, so the hand-off has no visible stall.
            double omega = 1.0 / SettleTau;
            double x = horizontalOffset - settleTargetH;
            double c = velocityX + (omega * x);
            double e = Math.Exp(-omega * dt);

            horizontalOffset = settleTargetH + ((x + (c * dt)) * e);
            velocityX = (c - (omega * (x + (c * dt)))) * e;

            if (Math.Abs(settleTargetH - horizontalOffset) < 0.5 && Math.Abs(velocityX) < FlingStopSpeed)
            {
                horizontalOffset = settleTargetH;
                StopMotion();
            }
        }
    }

    private void BeginRenderLoop()
    {
        if (!canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = true;
        }
    }

    private void StopMotion()
    {
        flinging = false;
        settling = false;
        velocityX = 0;
        velocityY = 0;

        // Drop back to on-demand painting so an idle view doesn't keep the GPU busy — unless the
        // draft pop-in is still playing, which also needs the loop.
        if (!panning && !OverlayAnimating() && canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = false;
        }

        PublishVerticalOffset();
    }

    private bool OverlayAnimating()
        => !double.IsNaN(typingAppearStart) && (clock.Elapsed.TotalSeconds - typingAppearStart) < TypingAppearDuration;

    private double ClampHorizontal(double offset)
    {
        float bodyWidth = (float)Width - RailWidth;
        return InfiniteScheduleGeometry.ClampOffset(offset, bodyWidth, dayWidth, DayIndex(MinDay), DayIndex(MaxDay));
    }

    private double ClampVerticalToView(double offset)
    {
        float bodyHeight = (float)Height - BodyTop;
        return ClampVertical(offset, bodyHeight, BuildTimeScale());
    }

    // Mirror the current scroll position onto the bindable VerticalOffset (for synced siblings),
    // guarding against the property-changed echo re-seating us.
    private void PublishVerticalOffset()
    {
        if (double.IsNaN(scrollY))
        {
            return;
        }

        suppressOffsetSync = true;
        VerticalOffset = scrollY;
        suppressOffsetSync = false;
    }

    private void OnVerticalOffsetChanged(double value)
    {
        if (suppressOffsetSync)
        {
            return;
        }

        scrollY = ClampVerticalToView(value);
        Invalidate();
    }

    private IDispatcherTimer CreateLongPressTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(450);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (!pressValid)
            {
                return;
            }

            longFired = true;

            // If the pressed appointment has quick-actions, show the menu instead of the long-tap:
            // Android pops it here; iOS shows it via the native UIContextMenuInteraction.
            var menuHit = HitTestMenuItem(new PointF((float)pressPoint.X, (float)pressPoint.Y));
            if (menuHit is { } mh && GetItemActions(mh.Item).Count > 0)
            {
#if ANDROID
                var menuItem = mh.Item;
                QuickActionMenu.ShowAndroid(canvas, new PointF((float)pressPoint.X, (float)pressPoint.Y), GetItemActions(menuItem), label => RaiseItemAction(menuItem, label));
#endif

                // The native menu / popup takes over the gesture and no touch-up reaches us, so the
                // held finger would linger in `touches` and make the next drag look like a pinch.
                ResetTouchState();
                return;
            }

            DispatchTap(pressPoint, longPress: true);

            // If the long-press handler just placed a holding or typing block under the finger, hand
            // the still-down gesture straight into a drag so the user can place it without lifting.
            if (interaction != InteractionKind.Scroll)
            {
                return;
            }

            if (HoldingSchedule is not null
                && HoldingScreenRect() is SKRect hr
                && new SKRect(hr.Left - HoldingHalo, hr.Top - HoldingHalo, hr.Right + HoldingHalo, hr.Bottom + HoldingHalo)
                    .Contains((float)pressPoint.X, (float)pressPoint.Y))
            {
                BeginBlockDrag(InteractionKind.Holding, pressPoint, hr);
            }
            else if (TypingItem is not null
                && TypingScreenRect() is SKRect tr
                && new SKRect(tr.Left - HoldingHalo, tr.Top - HoldingHalo, tr.Right + HoldingHalo, tr.Bottom + HoldingHalo)
                    .Contains((float)pressPoint.X, (float)pressPoint.Y))
            {
                BeginBlockDrag(InteractionKind.Typing, pressPoint, tr);
            }
        };
        return timer;
    }

    // Resolves a screen point to an item (or empty date/time) and raises the matching event.
    private void DispatchTap(Point screen, bool longPress)
    {
        // The all-day panel is painted in view space, so test it against the raw point.
        foreach (var (item, rect) in allDayHitMap)
        {
            if (rect.Contains((float)screen.X, (float)screen.Y))
            {
                RaiseTap(item, longPress);
                return;
            }
        }

        float localX = (float)(screen.X - RailWidth + horizontalOffset - (stripStartDay * dayWidth));
        float localY = (float)(screen.Y - BodyTop + scrollY);

        // A tap in the panel's empty space belongs to no time slot; swallow it.
        if (screen.Y < BodyTop)
        {
            return;
        }

        foreach (var (item, rect) in hitMap)
        {
            if (rect.Contains(localX, localY))
            {
                RaiseTap(item, longPress);
                return;
            }
        }

        float columnWidth = dayWidth / Math.Max(1, PersonCount);
        int dayIndex = stripStartDay + (int)Math.Floor(localX / dayWidth);
        var when = AnchorDay.Date.AddDays(dayIndex) + BuildTimeScale().TimeForY(localY);

        // Which person's sub-column was tapped (per-person mode) — the app binds a new draft to it.
        string? personId = null;
        if (Persons is { Count: > 1 } persons)
        {
            float withinDay = localX - ((dayIndex - stripStartDay) * dayWidth);
            int slot = Math.Clamp((int)Math.Floor(withinDay / columnWidth), 0, persons.Count - 1);
            personId = persons[slot].Id;
        }

        if (longPress)
        {
            LongTapped?.Invoke(this, new ScheduleTappedEventArgs(when, personId));
        }
        else
        {
            Tapped?.Invoke(this, new ScheduleTappedEventArgs(when, personId));
        }
    }

    private void RaiseTap(IScheduleItem item, bool longPress)
    {
        if (longPress)
        {
            ItemLongTapped?.Invoke(this, new ScheduleItemTappedEventArgs(item));
        }
        else
        {
            ItemTapped?.Invoke(this, new ScheduleItemTappedEventArgs(item));
        }
    }
}
#endif
