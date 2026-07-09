using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>Where <see cref="ScheduleView"/>'s header (and all-day panel) is rendered.</summary>
public enum ScheduleHeaderMode
{
    /// <summary>Header and all-day panel are drawn pinned inside the schedule control (default).</summary>
    Inhouse,

    /// <summary>
    /// Header and all-day panel are suppressed; an external <see cref="ScheduleHeaderView"/> whose
    /// <see cref="ScheduleHeaderView.Schedule"/> points at this view renders them instead — e.g. a
    /// translucent glass bar the full-bleed body scrolls under.
    /// </summary>
    Linked,

    /// <summary>No header at all; the all-day panel stays inside the schedule control.</summary>
    None,
}

/// <summary>
/// A standalone day/person header bar. Use it detached from <see cref="ScheduleView"/> — pinned at
/// the top of the screen (e.g. over an edge-to-edge schedule, iOS 26 liquid-glass style) — in one
/// of two modes:
/// <list type="bullet">
/// <item><b>Linked</b>: set <see cref="Schedule"/> (whose <see cref="ScheduleView.HeaderMode"/>
/// should be <see cref="ScheduleHeaderMode.Linked"/>). The header mirrors that schedule's columns,
/// theme, renderer and all-day bars, and follows its vertical scroll for the edge shadow. With a
/// <c>CarouselView</c> of schedules, point <see cref="Schedule"/> at the current page as it changes.</item>
/// <item><b>Standalone</b>: leave <see cref="Schedule"/> null and bind <see cref="StartDay"/>,
/// <see cref="EndDay"/>, <see cref="ViewMode"/>, <see cref="Persons"/> and <see cref="Theme"/> to
/// the same source as the schedule. Columns are built with the exact same rules, so they align.</item>
/// </list>
/// Column geometry matches the schedule as long as this view gets the same width and horizontal
/// insets as the schedule body. For glass, set <see cref="HeaderBackground"/> (the canvas then
/// paints on a transparent background) to a platform blur/glass view.
/// </summary>
public class ScheduleHeaderView : ContentView
{
    /// <summary>Bindable property for <see cref="Schedule"/>.</summary>
    public static readonly BindableProperty ScheduleProperty =
        BindableProperty.Create(
            nameof(Schedule),
            typeof(ScheduleView),
            typeof(ScheduleHeaderView),
            null,
            propertyChanged: (b, o, n) => ((ScheduleHeaderView)b).OnScheduleChanged(o as ScheduleView, n as ScheduleView));

    /// <summary>Bindable property for <see cref="StartDay"/>.</summary>
    public static readonly BindableProperty StartDayProperty =
        BindableProperty.Create(
            nameof(StartDay),
            typeof(DateTime),
            typeof(ScheduleHeaderView),
            DateTime.Today,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="EndDay"/>.</summary>
    public static readonly BindableProperty EndDayProperty =
        BindableProperty.Create(
            nameof(EndDay),
            typeof(DateTime),
            typeof(ScheduleHeaderView),
            DateTime.Today.AddDays(6),
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="ViewMode"/>.</summary>
    public static readonly BindableProperty ViewModeProperty =
        BindableProperty.Create(
            nameof(ViewMode),
            typeof(int),
            typeof(ScheduleHeaderView),
            7,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh(),
            coerceValue: (_, v) => Math.Clamp((int)v, 1, 7));

    /// <summary>Bindable property for <see cref="Persons"/>.</summary>
    public static readonly BindableProperty PersonsProperty =
        BindableProperty.Create(
            nameof(Persons),
            typeof(IList<IPerson>),
            typeof(ScheduleHeaderView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleViewTheme),
            typeof(ScheduleHeaderView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(ScheduleViewRenderer),
            typeof(ScheduleHeaderView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="ShowAllDay"/>.</summary>
    public static readonly BindableProperty ShowAllDayProperty =
        BindableProperty.Create(
            nameof(ShowAllDay),
            typeof(bool),
            typeof(ScheduleHeaderView),
            true,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="DrawsBackground"/>.</summary>
    public static readonly BindableProperty DrawsBackgroundProperty =
        BindableProperty.Create(
            nameof(DrawsBackground),
            typeof(bool),
            typeof(ScheduleHeaderView),
            true,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).Refresh());

    /// <summary>Bindable property for <see cref="HeaderBackground"/>.</summary>
    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(
            nameof(HeaderBackground),
            typeof(View),
            typeof(ScheduleHeaderView),
            null,
            propertyChanged: (b, o, n) => ((ScheduleHeaderView)b).OnHeaderBackgroundChanged(o as View, n as View));

    /// <summary>Bindable property for <see cref="ScrollOffset"/>.</summary>
    public static readonly BindableProperty ScrollOffsetProperty =
        BindableProperty.Create(
            nameof(ScrollOffset),
            typeof(double),
            typeof(ScheduleHeaderView),
            0.0,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).UpdateEdgeShadow());

    /// <summary>Bindable property for <see cref="ShowsScrollEdgeShadow"/>.</summary>
    public static readonly BindableProperty ShowsScrollEdgeShadowProperty =
        BindableProperty.Create(
            nameof(ShowsScrollEdgeShadow),
            typeof(bool),
            typeof(ScheduleHeaderView),
            true,
            propertyChanged: (b, _, _) => ((ScheduleHeaderView)b).UpdateEdgeShadow());

    // Scroll distance over which the edge shadow fades in.
    private const float EdgeShadowRampPixels = 24f;

    private const float EdgeShadowMaxOpacity = 0.22f;

    private readonly ScheduleViewTheme fallbackTheme = new ScheduleViewTheme();

    private readonly ScheduleRenderContext ownContext = new ScheduleRenderContext();

    private readonly ScheduleHeaderDrawable headerDrawable;

    private readonly ScheduleAllDayDrawable allDayDrawable;

    private readonly GraphicsView headerCanvas;

    private readonly GraphicsView allDayCanvas;

    // Hidden canvases must collapse via their row heights: a hidden child with HeightRequest=0
    // still leaves the Auto row a few points tall (stray strip under the day bar).
    private readonly RowDefinition dayRow = new RowDefinition { Height = GridLength.Auto };

    private readonly RowDefinition allDayRow = new RowDefinition { Height = GridLength.Auto };

    private readonly Grid root;

    private readonly Shadow edgeShadow;

    /// <summary>Initializes a new instance of the <see cref="ScheduleHeaderView"/> class.</summary>
    public ScheduleHeaderView()
    {
        headerDrawable = new ScheduleHeaderDrawable { Context = ownContext };
        headerCanvas = new GraphicsView
        {
            Drawable = headerDrawable,
            BackgroundColor = Colors.Transparent,
            InputTransparent = true,
        };

        allDayDrawable = new ScheduleAllDayDrawable { Context = ownContext };
        allDayCanvas = new GraphicsView
        {
            Drawable = allDayDrawable,
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
        };
        var allDayTap = new TapGestureRecognizer();
        allDayTap.Tapped += OnAllDayTapped;
        allDayCanvas.GestureRecognizers.Add(allDayTap);

        root = new Grid
        {
            RowDefinitions =
            {
                dayRow,
                allDayRow,
            },
        };
        root.Children.Add(headerCanvas);
        Grid.SetRow(headerCanvas, 0);
        root.Children.Add(allDayCanvas);
        Grid.SetRow(allDayCanvas, 1);
        Content = root;

        edgeShadow = new Shadow
        {
            Brush = new SolidColorBrush(Colors.Black),
            Offset = new Point(0, 2),
            Radius = 8,
            Opacity = 0f,
        };
        Shadow = edgeShadow;

        Loaded += (_, _) => Refresh();
    }

    /// <summary>Fired when the user taps an all-day / cross-date bar.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemTapped;

    /// <summary>
    /// Schedule to mirror. When set, this header renders that view's columns, theme, renderer and
    /// all-day bars (the standalone properties below are ignored) and tracks its vertical scroll.
    /// Set the schedule's <see cref="ScheduleView.HeaderMode"/> to
    /// <see cref="ScheduleHeaderMode.Linked"/> so it stops drawing its own header.
    /// </summary>
    public ScheduleView? Schedule
    {
        get => (ScheduleView?)GetValue(ScheduleProperty);
        set => SetValue(ScheduleProperty, value);
    }

    /// <summary>First day rendered (inclusive). Standalone mode only.</summary>
    public DateTime StartDay
    {
        get => (DateTime)GetValue(StartDayProperty);
        set => SetValue(StartDayProperty, value);
    }

    /// <summary>Last day rendered (inclusive). Standalone mode only.</summary>
    public DateTime EndDay
    {
        get => (DateTime)GetValue(EndDayProperty);
        set => SetValue(EndDayProperty, value);
    }

    /// <summary>Maximum columns to display (1..7). Standalone mode only.</summary>
    public int ViewMode
    {
        get => (int)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>Optional persons; when non-empty each day splits per person. Standalone mode only.</summary>
    public IList<IPerson>? Persons
    {
        get => (IList<IPerson>?)GetValue(PersonsProperty);
        set => SetValue(PersonsProperty, value);
    }

    /// <summary>Theme bundle. Standalone mode only (linked mode uses the schedule's theme).</summary>
    public ScheduleViewTheme Theme
    {
        get => (ScheduleViewTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Custom painter. Standalone mode only (linked mode uses the schedule's renderer).</summary>
    public ScheduleViewRenderer Renderer
    {
        get => (ScheduleViewRenderer)GetValue(RendererProperty) ?? ScheduleViewRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    /// <summary>Whether the all-day / cross-date panel renders below the day bar (linked mode only — standalone headers have no items).</summary>
    public bool ShowAllDay
    {
        get => (bool)GetValue(ShowAllDayProperty);
        set => SetValue(ShowAllDayProperty, value);
    }

    /// <summary>
    /// Whether the canvases paint the opaque theme background. Automatically skipped while
    /// <see cref="HeaderBackground"/> is set, so the glass/blur view shows through.
    /// </summary>
    public bool DrawsBackground
    {
        get => (bool)GetValue(DrawsBackgroundProperty);
        set => SetValue(DrawsBackgroundProperty, value);
    }

    /// <summary>
    /// Optional view layered behind the canvases — typically a platform blur / liquid-glass view.
    /// Setting it makes the canvases paint on a transparent background.
    /// </summary>
    public View? HeaderBackground
    {
        get => (View?)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>
    /// Vertical scroll offset of the content under this header; drives the scroll-edge shadow.
    /// Auto-tracks <see cref="ScheduleView.VerticalOffset"/> while <see cref="Schedule"/> is set.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>Whether a soft shadow appears under the header once content is scrolled beneath it.</summary>
    public bool ShowsScrollEdgeShadow
    {
        get => (bool)GetValue(ShowsScrollEdgeShadowProperty);
        set => SetValue(ShowsScrollEdgeShadowProperty, value);
    }

    private ScheduleRenderContext ActiveContext => Schedule?.RenderContext ?? ownContext;

    private void OnScheduleChanged(ScheduleView? oldValue, ScheduleView? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Rebuilt -= OnScheduleRebuilt;
            oldValue.PropertyChanged -= OnSchedulePropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.Rebuilt += OnScheduleRebuilt;
            newValue.PropertyChanged += OnSchedulePropertyChanged;
            ScrollOffset = newValue.VerticalOffset;
        }

        Refresh();
    }

    private void OnScheduleRebuilt(object? sender, EventArgs e) => Refresh();

    private void OnSchedulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduleView.VerticalOffset) && Schedule is { } schedule)
        {
            ScrollOffset = schedule.VerticalOffset;
        }
    }

    private void OnHeaderBackgroundChanged(View? oldValue, View? newValue)
    {
        if (oldValue is not null)
        {
            root.Children.Remove(oldValue);
        }

        if (newValue is not null)
        {
            root.Children.Insert(0, newValue);
            Grid.SetRow(newValue, 0);
            Grid.SetRowSpan(newValue, 2);
        }

        Refresh();
    }

    // Re-reads the active context (rebuilding it first in standalone mode) and re-lays-out the rows.
    private void Refresh()
    {
        var schedule = Schedule;
        if (schedule is null)
        {
            RebuildOwnContext();
        }

        var ctx = ActiveContext;
        var renderer = schedule?.Renderer ?? Renderer;
        bool transparent = !DrawsBackground || HeaderBackground is not null;

        headerDrawable.Context = ctx;
        headerDrawable.Renderer = renderer;
        headerDrawable.DrawsBackground = !transparent;
        allDayDrawable.Context = ctx;
        allDayDrawable.Renderer = renderer;
        allDayDrawable.DrawsBackground = !transparent;

        bool hasColumns = ctx.Columns.Count > 0;
        headerCanvas.HeightRequest = hasColumns ? ctx.HeaderHeight : 0;
        headerCanvas.IsVisible = hasColumns && ctx.HeaderHeight > 0;
        dayRow.Height = headerCanvas.IsVisible ? GridLength.Auto : new GridLength(0);

        int laneCount = 0;
        foreach (var bar in ctx.AllDayBars)
        {
            laneCount = Math.Max(laneCount, bar.Lane + 1);
        }

        float panelHeight = laneCount > 0 ? (laneCount * ctx.AllDayLaneHeight) + 6f : 0f;
        allDayCanvas.HeightRequest = ShowAllDay ? panelHeight : 0;
        allDayCanvas.IsVisible = ShowAllDay && panelHeight > 0;
        allDayRow.Height = allDayCanvas.IsVisible ? GridLength.Auto : new GridLength(0);

        headerCanvas.Invalidate();
        allDayCanvas.Invalidate();
        UpdateEdgeShadow();
    }

    private void RebuildOwnContext()
    {
        var theme = Theme;
        ownContext.Theme = theme;
        ownContext.TimeRailWidth = (float)theme.TimeRailWidth;
        ownContext.HeaderHeight = (float)theme.HeaderHeight;
        ownContext.Columns = ScheduleColumnBuilder.Build(StartDay, EndDay, ViewMode, Persons);
        ownContext.DayCount = ScheduleColumnBuilder.EffectiveDays(StartDay, EndDay, ViewMode);
    }

    private void UpdateEdgeShadow()
    {
        edgeShadow.Opacity = ShowsScrollEdgeShadow
            ? Math.Clamp((float)(ScrollOffset / EdgeShadowRampPixels), 0f, 1f) * EdgeShadowMaxOpacity
            : 0f;
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
}
