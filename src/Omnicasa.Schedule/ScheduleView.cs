using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
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
    public ScheduleTappedEventArgs(DateTime when)
    {
        When = when;
    }

    /// <summary>Gets the date and time at the tap location (day of the column + time-of-day at the tap Y).</summary>
    public DateTime When { get; }
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

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleViewTheme),
            typeof(ScheduleView),
            null,
            propertyChanged: (b, _, _) => ((ScheduleView)b).Rebuild());

    /// <summary>Bindable property for <see cref="Persons"/>.</summary>
    public static readonly BindableProperty PersonsProperty =
        BindableProperty.Create(
            nameof(Persons),
            typeof(IList<Person>),
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

    private readonly ScheduleViewTheme fallbackTheme = new ScheduleViewTheme();

    private readonly ScheduleRenderContext context = new ScheduleRenderContext();

    private readonly ScheduleBodyDrawable bodyDrawable;

    private readonly GraphicsView headerCanvas;

    private readonly GraphicsView bodyCanvas;

    private readonly ScrollView bodyScroll;

    private readonly IDispatcherTimer longPressTimer;

    private PointF pointerDownPoint;

    private bool pointerDown;

    private bool longTapFired;

    private bool moveCanceledTap;

    private double pinchBase = 60;

    private double pinchScale = 1.0;

    private double pinchAnchorHours = -1;

    private double pinchAnchorViewportY;

    /// <summary>Initializes a new instance of the <see cref="ScheduleView"/> class.</summary>
    public ScheduleView()
    {
        bodyDrawable = new ScheduleBodyDrawable { Context = context };

        headerCanvas = new GraphicsView
        {
            Drawable = new ScheduleHeaderDrawable { Context = context },
            BackgroundColor = Colors.Transparent,
            InputTransparent = true,
        };

        bodyCanvas = new GraphicsView
        {
            Drawable = bodyDrawable,
            BackgroundColor = Colors.Transparent,
        };

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
        bodyScroll.Scrolled += (_, _) => CancelPendingLongPress();

        longPressTimer = Dispatcher.CreateTimer();
        longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressMilliseconds);
        longPressTimer.IsRepeating = false;
        longPressTimer.Tick += OnLongPressTick;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
        };
        root.Children.Add(headerCanvas);
        Grid.SetRow(headerCanvas, 0);
        root.Children.Add(bodyScroll);
        Grid.SetRow(bodyScroll, 1);
        Content = root;

        Loaded += (_, _) => Rebuild();
    }

    /// <summary>Fired when the user taps an empty area of the body. Payload is the day + time of the tap.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? Tapped;

    /// <summary>Fired when the user taps an appointment block.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemTapped;

    /// <summary>Fired when the user long-presses an empty area. Payload is the day + time at the press.</summary>
    public event EventHandler<ScheduleTappedEventArgs>? LongTapped;

    /// <summary>Fired when the user long-presses an appointment block.</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemLongTapped;

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

    /// <summary>Theme bundle (colors + font sizes). Defaults to a built-in light theme.</summary>
    public ScheduleViewTheme Theme
    {
        get => (ScheduleViewTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Optional persons. When non-empty, each day splits into one sub-column per person.</summary>
    public IList<Person>? Persons
    {
        get => (IList<Person>?)GetValue(PersonsProperty);
        set => SetValue(PersonsProperty, value);
    }

    /// <summary>Items rendered.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(
                char.ToUpperInvariant(parts[0][0]),
                char.ToUpperInvariant(parts[^1][0]));
        }

        var single = parts[0];
        return single.Length >= 2
            ? single.Substring(0, 2).ToUpperInvariant()
            : single.ToUpperInvariant();
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

    private void Rebuild()
    {
        var theme = Theme;
        var personsMode = Persons is not null && Persons.Count > 0;
        int personCount = personsMode ? Persons!.Count : 1;

        var rangeStart = StartDay.Date;
        var rangeEnd = EndDay.Date;
        if (rangeEnd < rangeStart)
        {
            rangeEnd = rangeStart;
        }

        int rangeDays = (int)(rangeEnd - rangeStart).TotalDays + 1;
        int days = Math.Min(Math.Max(1, ViewMode), rangeDays);

        float headerHeight = (personsMode || days > 1) ? (float)theme.HeaderHeight : 0f;
        context.Theme = theme;
        context.TimeRailWidth = (float)theme.TimeRailWidth;
        context.HeaderHeight = headerHeight;
        context.Scale = new TimeScale((float)HourHeight);

        headerCanvas.HeightRequest = headerHeight;
        headerCanvas.IsVisible = headerHeight > 0;
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        var columns = new ScheduleViewColumn[days * personCount];

        for (int d = 0; d < days; d++)
        {
            var dayStart = rangeStart.AddDays(d);
            var dayEnd = dayStart.AddDays(1);
            var dayOnly = DateOnly.FromDateTime(dayStart);
            var dayShort = dayOnly.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
            var dayNum = dayOnly.Day.ToString(CultureInfo.InvariantCulture);
            var isToday = dayOnly == today;

            var dayItems = items
                .Where(a => a.Start < dayEnd && a.End > dayStart && !a.IsAllDay)
                .ToList();

            if (personsMode)
            {
                for (int p = 0; p < personCount; p++)
                {
                    var person = Persons![p];
                    var forPerson = dayItems
                        .Where(a => string.Equals(a.PersonId, person.Id, StringComparison.Ordinal))
                        .ToList();
                    columns[(d * personCount) + p] = new ScheduleViewColumn
                    {
                        DayStart = dayStart,
                        HeaderPrimary = $"{dayShort} {dayNum}",
                        HeaderSecondary = Initials(person.Name),
                        Accent = person.Color,
                        IsToday = isToday,
                        Items = ScheduleLayout.Layout(forPerson),
                    };
                }
            }
            else
            {
                columns[d] = new ScheduleViewColumn
                {
                    DayStart = dayStart,
                    HeaderPrimary = dayShort,
                    HeaderSecondary = days > 1 ? dayNum : null,
                    Accent = null,
                    IsToday = isToday,
                    Items = ScheduleLayout.Layout(dayItems),
                };
            }
        }

        context.Columns = columns;
        context.Now = DateTime.Now;

        headerCanvas.Invalidate();
        bodyCanvas.Invalidate();
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(bodyCanvas);
        if (pt is null)
        {
            return;
        }

        pointerDownPoint = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        pointerDown = true;
        longTapFired = false;
        moveCanceledTap = false;
        longPressTimer.Stop();
        longPressTimer.Start();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!pointerDown)
        {
            return;
        }

        var pt = e.GetPosition(bodyCanvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        if (Math.Abs(p.X - pointerDownPoint.X) > LongPressMoveThreshold
            || Math.Abs(p.Y - pointerDownPoint.Y) > LongPressMoveThreshold)
        {
            moveCanceledTap = true;
            longPressTimer.Stop();
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
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
        CancelPendingLongPress();
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
                    ItemLongTapped?.Invoke(this, args);
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
                    context.Scale = new TimeScale((float)target);
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
