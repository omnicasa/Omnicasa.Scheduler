using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Omnicasa.Schedule;

/// <summary>A one-or-more day (or one-day, N-person) page hosted inside <see cref="DayAgendaView"/>'s carousel.</summary>
internal class DayAgendaPage : ContentView
{
    private const float MultiColumnHeaderHeight = 48f;

    private readonly DayAgendaView host;

    private readonly DayAgendaDrawable drawable = new DayAgendaDrawable();

    private readonly GraphicsView canvas;

    private readonly ScrollView scroll;

    private readonly Border moveHandle;

    private readonly Border resizeHandle;

    private Appointment? selected;

    private Appointment? dragging;

    private DragKind dragKind;

    private DateTime dragOriginStart;

    private DateTime dragOriginEnd;

    private DateTime dragDay;

    private CancellationTokenSource? loadCts;

    private DateTime? anchorDay;

    private int daysInPage = 1;

    private int expectedColumnCount = 1;

    private double pinchBase = 60;

    private double pinchScale = 1.0;

    private double pinchAnchorHours = -1;

    private double pinchAnchorViewportY;

    private bool suppressScrollSync;

    private IDispatcherTimer? longPressTimer;

    private PointF longPressStartPoint;

    private bool longPressActive;

    /// <summary>Initializes a new instance of the <see cref="DayAgendaPage"/> class.</summary>
    /// <param name="host">The parent <see cref="DayAgendaView"/> that owns shared state.</param>
    public DayAgendaPage(DayAgendaView host)
    {
        this.host = host;

        drawable.Theme = host.Theme;
        drawable.Renderer = host.Renderer;

        canvas = new GraphicsView
        {
            Drawable = drawable,
            BackgroundColor = Colors.Transparent,
        };

        SyncConfigFromHost();

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnCanvasTapped;
        canvas.GestureRecognizers.Add(tap);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        canvas.GestureRecognizers.Add(pinch);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += OnPointerPressed;
        pointer.PointerMoved += OnPointerMoved;
        pointer.PointerReleased += OnPointerReleased;
        pointer.PointerExited += OnPointerReleased;
        canvas.GestureRecognizers.Add(pointer);

        longPressTimer = Dispatcher.CreateTimer();
        longPressTimer.Interval = TimeSpan.FromMilliseconds(400);
        longPressTimer.IsRepeating = false;
        longPressTimer.Tick += OnLongPressTick;

        scroll = new ScrollView
        {
            Content = canvas,
            Orientation = ScrollOrientation.Vertical,
        };
        scroll.Scrolled += OnScrolled;
        scroll.SizeChanged += OnLayoutSettled;
        canvas.SizeChanged += OnLayoutSettled;

        moveHandle = BuildHandle(DragKind.Move);
        resizeHandle = BuildHandle(DragKind.Resize);
        var overlay = new AbsoluteLayout { InputTransparent = true };
        overlay.Children.Add(moveHandle);
        overlay.Children.Add(resizeHandle);
        moveHandle.IsVisible = false;
        resizeHandle.IsVisible = false;

        var root = new Grid();
        root.Children.Add(scroll);
        root.Children.Add(overlay);
        Content = root;

        host.HourHeightChanged += OnHourHeightChanged;
        host.ThemeChanged += OnThemeChanged;
        host.RendererChanged += OnRendererChanged;
        host.SourceChanged += OnSourceChanged;
        host.SharedScrollYChanged += OnSharedScrollYChanged;
        host.DaysPerPageChanged += OnConfigChanged;
        host.PersonsChanged += OnConfigChanged;
        drawable.Drawn += OnDrawableDrawn;
        BindingContextChanged += OnBindingContextChanged;
        Loaded += (_, _) =>
        {
            SyncConfigFromHost();
            if (anchorDay is not null)
            {
                _ = ReloadAsync();
            }

            InitialScroll();
        };
        Unloaded += (_, _) =>
        {
            host.HourHeightChanged -= OnHourHeightChanged;
            host.ThemeChanged -= OnThemeChanged;
            host.RendererChanged -= OnRendererChanged;
            host.SourceChanged -= OnSourceChanged;
            host.SharedScrollYChanged -= OnSharedScrollYChanged;
            host.DaysPerPageChanged -= OnConfigChanged;
            host.PersonsChanged -= OnConfigChanged;
            drawable.Drawn -= OnDrawableDrawn;
            BindingContextChanged -= OnBindingContextChanged;
        };
    }

    private enum DragKind
    {
        Move,
        Resize,
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
        if (single.Length >= 2)
        {
            return single.Substring(0, 2).ToUpperInvariant();
        }

        return single.ToUpperInvariant();
    }

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

    private Border BuildHandle(DragKind kind)
    {
        var b = new Border
        {
            BackgroundColor = host.Theme.Accent,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 0,
            WidthRequest = 16,
            HeightRequest = 16,
            InputTransparent = false,
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.25f,
                Radius = 4,
                Offset = new Point(0, 1),
            },
        };
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, e) => OnHandlePan(kind, e);
        b.GestureRecognizers.Add(pan);
        return b;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is DateTime d)
        {
            anchorDay = d.Date;
            SyncConfigFromHost();
            ClearSelection();
            _ = ReloadAsync();
        }
    }

    private void OnConfigChanged()
    {
        SyncConfigFromHost();
        ClearSelection();
        _ = ReloadAsync();
    }

    private void SyncConfigFromHost()
    {
        bool personsMode = host.Persons is not null && host.Persons.Count > 0;
        daysInPage = Math.Max(1, host.DaysPerPage);
        int personCount = personsMode ? host.Persons!.Count : 1;
        expectedColumnCount = daysInPage * personCount;
        var header = personsMode || daysInPage > 1 ? MultiColumnHeaderHeight : 0f;
        drawable.HeaderHeight = header;
        drawable.Scale = new TimeScale((float)host.HourHeight, header);
        canvas.HeightRequest = drawable.Scale.TotalHeight;
    }

    private void OnHourHeightChanged()
    {
        drawable.Scale = new TimeScale((float)host.HourHeight, drawable.HeaderHeight);
        canvas.HeightRequest = drawable.Scale.TotalHeight;
        PositionHandles();
        canvas.Invalidate();
    }

    private void OnThemeChanged()
    {
        drawable.Theme = host.Theme;
        moveHandle.BackgroundColor = host.Theme.Accent;
        resizeHandle.BackgroundColor = host.Theme.Accent;
        canvas.Invalidate();
    }

    private void OnRendererChanged()
    {
        drawable.Renderer = host.Renderer;
        canvas.Invalidate();
    }

    private void OnSourceChanged(IAppointmentSource? source) => _ = ReloadAsync();

    private async Task ReloadAsync()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        if (anchorDay is null)
        {
            drawable.Columns = Array.Empty<AgendaColumn>();
            canvas.Invalidate();
            return;
        }

        bool personsMode = host.Persons is not null && host.Persons.Count > 0;
        int days = daysInPage;
        var from = anchorDay.Value;
        var rangeEnd = from.AddDays(days).AddTicks(-1);

        if (host.AppointmentSource is null)
        {
            drawable.Columns = BuildColumns(personsMode, from, days, Array.Empty<Appointment>());
            canvas.Invalidate();
            return;
        }

        try
        {
            var list = await host.AppointmentSource.GetAsync(from, rangeEnd, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            drawable.Columns = BuildColumns(personsMode, from, days, list);
            drawable.Now = DateTime.Now;
            canvas.Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private AgendaColumn[] BuildColumns(bool personsMode, DateTime from, int days, IReadOnlyList<Appointment> list)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var persons = personsMode ? host.Persons! : null;
        int personCount = persons?.Count ?? 1;
        var result = new AgendaColumn[days * personCount];

        for (int d = 0; d < days; d++)
        {
            var dayStart = from.AddDays(d);
            var dayEnd = dayStart.AddDays(1);
            var dayOnly = DateOnly.FromDateTime(dayStart);
            var dayShort = dayOnly.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
            var dayNum = dayOnly.Day.ToString(CultureInfo.InvariantCulture);
            var isToday = dayOnly == today;
            var forDay = list.Where(a => a.Start < dayEnd && a.End > dayStart && !a.IsAllDay).ToList();

            if (personsMode)
            {
                for (int p = 0; p < personCount; p++)
                {
                    var person = persons![p];
                    var forPerson = forDay.Where(a => string.Equals(a.PersonId, person.Id, StringComparison.Ordinal)).ToList();
                    result[(d * personCount) + p] = new AgendaColumn
                    {
                        DayStart = dayStart,
                        HeaderPrimary = $"{dayShort} {dayNum}",
                        HeaderSecondary = Initials(person.Name),
                        Accent = person.Color,
                        IsToday = isToday,
                        Events = EventLayoutEngine.Layout(forPerson).ToList(),
                    };
                }
            }
            else
            {
                result[d] = new AgendaColumn
                {
                    DayStart = dayStart,
                    HeaderPrimary = dayShort,
                    HeaderSecondary = days > 1 ? dayNum : null,
                    Accent = null,
                    IsToday = isToday,
                    Events = EventLayoutEngine.Layout(forDay).ToList(),
                };
            }
        }

        return result;
    }

    private void InitialScroll()
    {
        if (double.IsNaN(host.SharedScrollY))
        {
            var hour = Math.Max(0, DateTime.Now.Hour - 1);
            var y = drawable.Scale.YForTime(TimeSpan.FromHours(hour));
            host.UpdateSharedScrollY(y);
        }

        TryApplySharedScroll();
    }

    private void OnLayoutSettled(object? sender, EventArgs e)
    {
        TryApplySharedScroll();
        if (anchorDay is not null && drawable.Columns.Count != expectedColumnCount)
        {
            _ = ReloadAsync();
        }
    }

    private void TryApplySharedScroll()
    {
        if (suppressScrollSync)
        {
            return;
        }

        if (double.IsNaN(host.SharedScrollY))
        {
            return;
        }

        if (scroll.Height <= 0 || canvas.Height <= 0)
        {
            return;
        }

        if (Math.Abs(scroll.ScrollY - host.SharedScrollY) < 0.5)
        {
            return;
        }

        _ = ScrollToSilently(host.SharedScrollY);
    }

    private async Task ScrollToSilently(double y)
    {
        suppressScrollSync = true;
        try
        {
            await scroll.ScrollToAsync(0, y, false);
        }
        finally
        {
            suppressScrollSync = false;
        }
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (suppressScrollSync)
        {
            return;
        }

        host.UpdateSharedScrollY(e.ScrollY);
    }

    private async void OnSharedScrollYChanged(double y)
    {
        if (Math.Abs(scroll.ScrollY - y) < 0.5)
        {
            return;
        }

        await ScrollToSilently(y);
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                pinchBase = host.HourHeight;
                pinchScale = 1.0;
                pinchAnchorHours = -1;
                suppressScrollSync = true;
                moveHandle.IsVisible = false;
                resizeHandle.IsVisible = false;
                break;

            case GestureStatus.Running:
                if (pinchAnchorHours < 0)
                {
                    CaptureAnchor(e.ScaleOrigin.Y);
                }

                pinchScale *= e.Scale;
                var target = Math.Clamp(pinchBase * pinchScale, 24, 200);
                if (Math.Abs(target - drawable.Scale.HourHeight) >= 1.0)
                {
                    ApplyLocalHourHeight(target);
                    KeepAnchorPinned();
                }

                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var final = Math.Clamp(pinchBase * pinchScale, 24, 200);
                pinchScale = 1.0;
                pinchAnchorHours = -1;
                host.HourHeight = final;
                var committedY = scroll.ScrollY;
                suppressScrollSync = false;
                host.UpdateSharedScrollY(committedY);
                PositionHandles();
                break;
        }
    }

    private void CaptureAnchor(double originFraction)
    {
        var oldTotal = drawable.Scale.TotalHeight;
        var anchorCanvasY = originFraction * oldTotal;
        pinchAnchorHours = drawable.Scale.TimeForY((float)anchorCanvasY).TotalHours;
        pinchAnchorViewportY = anchorCanvasY - scroll.ScrollY;
    }

    private void KeepAnchorPinned()
    {
        var newAnchorCanvasY = drawable.Scale.YForTime(TimeSpan.FromHours(pinchAnchorHours));
        var newScrollY = newAnchorCanvasY - pinchAnchorViewportY;
        var maxScroll = Math.Max(0, drawable.Scale.TotalHeight - scroll.Height);
        newScrollY = Math.Clamp(newScrollY, 0, maxScroll);
        _ = scroll.ScrollToAsync(0, newScrollY, false);
    }

    private void ApplyLocalHourHeight(double h)
    {
        drawable.Scale = new TimeScale((float)h, drawable.HeaderHeight);
        canvas.HeightRequest = drawable.Scale.TotalHeight;
        canvas.Invalidate();
    }

    private void OnCanvasTapped(object? sender, TappedEventArgs e)
    {
        var pt = e.GetPosition(canvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        foreach (var (a, rect) in drawable.HitMap)
        {
            if (rect.Contains(p))
            {
                selected = a;
                PositionHandles();
                host.RaiseAppointmentTapped(a);
                return;
            }
        }

        ClearSelection();
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(canvas);
        if (pt is null)
        {
            return;
        }

        longPressStartPoint = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        longPressActive = false;
        longPressTimer?.Stop();
        longPressTimer?.Start();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(canvas);
        if (pt is null)
        {
            return;
        }

        var p = new PointF((float)pt.Value.X, (float)pt.Value.Y);
        if (!longPressActive)
        {
            if (Math.Abs(p.X - longPressStartPoint.X) > 12 || Math.Abs(p.Y - longPressStartPoint.Y) > 12)
            {
                longPressTimer?.Stop();
            }

            return;
        }

        if (dragging is null)
        {
            return;
        }

        int targetCol = HitTestColumn(p.X);
        if (targetCol < 0)
        {
            return;
        }

        var targetDayStart = drawable.Columns[targetCol].DayStart;
        var duration = dragOriginEnd - dragOriginStart;
        var dy = p.Y - longPressStartPoint.Y;
        var minutes = Math.Round(dy / host.HourHeight * 60.0 / 15.0) * 15.0;
        var tentative = targetDayStart.Add(dragOriginStart.TimeOfDay).AddMinutes(minutes);
        var newStart = ClampToDay(tentative, targetDayStart, duration);

        drawable.GhostStart = newStart;
        drawable.GhostEnd = newStart + duration;
        drawable.GhostColumnIndex = targetCol;
        dragDay = targetDayStart;
        canvas.Invalidate();
    }

    private int HitTestColumn(float x)
    {
        int n = drawable.Columns.Count;
        if (n == 0)
        {
            return -1;
        }

        float railWidth = drawable.TimeRailWidth;
        float width = (float)canvas.Width;
        if (width <= railWidth)
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
        longPressTimer?.Stop();
        if (!longPressActive)
        {
            return;
        }

        longPressActive = false;
        scroll.Orientation = ScrollOrientation.Vertical;
        host.SetSwipeEnabled(true);
        if (dragging is not null
            && drawable.GhostStart is { } gs
            && drawable.GhostEnd is { } ge)
        {
            dragging.Start = gs;
            dragging.End = ge;

            if (drawable.GhostColumnIndex is int idx && idx >= 0 && idx < drawable.Columns.Count)
            {
                int personCount = host.Persons is { Count: > 0 } ? host.Persons.Count : 0;
                if (personCount > 0)
                {
                    var person = host.Persons![idx % personCount];
                    dragging.PersonId = person.Id;
                }
            }

            host.RaiseAppointmentChanged(dragging);
            _ = ReloadAsync();
        }

        drawable.Ghost = null;
        drawable.GhostStart = null;
        drawable.GhostEnd = null;
        drawable.GhostColumnIndex = null;
        dragging = null;
        canvas.Invalidate();
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        longPressTimer?.Stop();
        foreach (var (a, rect) in drawable.HitMap)
        {
            if (!rect.Contains(longPressStartPoint))
            {
                continue;
            }

            longPressActive = true;
            selected = a;
            dragging = a;
            dragKind = DragKind.Move;
            dragOriginStart = a.Start;
            dragOriginEnd = a.End;
            dragDay = a.Start.Date;
            scroll.Orientation = ScrollOrientation.Neither;
            host.SetSwipeEnabled(false);
            drawable.Ghost = a;
            drawable.GhostStart = a.Start;
            drawable.GhostEnd = a.End;
            moveHandle.IsVisible = false;
            resizeHandle.IsVisible = false;
            canvas.Invalidate();
            return;
        }
    }

    private void OnDrawableDrawn()
    {
        if (selected is not null && dragging is null)
        {
            PositionHandles();
        }
    }

    private void ClearSelection()
    {
        selected = null;
        moveHandle.IsVisible = false;
        resizeHandle.IsVisible = false;
    }

    private void PositionHandles()
    {
        if (selected is null)
        {
            moveHandle.IsVisible = false;
            resizeHandle.IsVisible = false;
            return;
        }

        RectF? rect = null;
        foreach (var (a, r) in drawable.HitMap)
        {
            if (ReferenceEquals(a, selected))
            {
                rect = r;
                break;
            }
        }

        if (rect is null)
        {
            ClearSelection();
            return;
        }

        double canvasTop = -scroll.ScrollY;
        double x = rect.Value.X + rect.Value.Width - 18;
        double moveY = canvasTop + rect.Value.Y - 8;
        double resizeY = canvasTop + rect.Value.Bottom - 8;

        AbsoluteLayout.SetLayoutBounds(moveHandle, new Rect(x, moveY, 16, 16));
        AbsoluteLayout.SetLayoutFlags(moveHandle, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(resizeHandle, new Rect(x, resizeY, 16, 16));
        AbsoluteLayout.SetLayoutFlags(resizeHandle, AbsoluteLayoutFlags.None);
        moveHandle.IsVisible = true;
        resizeHandle.IsVisible = true;
    }

    private void OnHandlePan(DragKind kind, PanUpdatedEventArgs e)
    {
        if (selected is null)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                dragging = selected;
                dragKind = kind;
                dragOriginStart = selected.Start;
                dragOriginEnd = selected.End;
                dragDay = selected.Start.Date;
                scroll.Orientation = ScrollOrientation.Neither;
                host.SetSwipeEnabled(false);
                drawable.Ghost = selected;
                drawable.GhostStart = selected.Start;
                drawable.GhostEnd = selected.End;
                break;

            case GestureStatus.Running:
                if (dragging is null)
                {
                    break;
                }

                var dy = e.TotalY;
                var minutes = Math.Round(dy / host.HourHeight * 60.0 / 15.0) * 15.0;
                if (kind == DragKind.Move)
                {
                    var duration = dragOriginEnd - dragOriginStart;
                    var newStart = ClampToDay(dragOriginStart.AddMinutes(minutes), dragDay, duration);
                    drawable.GhostStart = newStart;
                    drawable.GhostEnd = newStart + duration;
                }
                else
                {
                    var newEnd = dragOriginEnd.AddMinutes(minutes);
                    var min = dragOriginStart.AddMinutes(15);
                    if (newEnd < min)
                    {
                        newEnd = min;
                    }

                    var dayEnd = dragDay.AddDays(1);
                    if (newEnd > dayEnd)
                    {
                        newEnd = dayEnd;
                    }

                    drawable.GhostEnd = newEnd;
                }

                canvas.Invalidate();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                scroll.Orientation = ScrollOrientation.Vertical;
                host.SetSwipeEnabled(true);
                if (dragging is not null
                    && drawable.GhostStart is { } gs
                    && drawable.GhostEnd is { } ge
                    && e.StatusType == GestureStatus.Completed)
                {
                    dragging.Start = gs;
                    dragging.End = ge;
                    host.RaiseAppointmentChanged(dragging);
                    _ = ReloadAsync();
                }

                drawable.Ghost = null;
                drawable.GhostStart = null;
                drawable.GhostEnd = null;
                drawable.GhostColumnIndex = null;
                dragging = null;
                canvas.Invalidate();
                break;
        }
    }
}
