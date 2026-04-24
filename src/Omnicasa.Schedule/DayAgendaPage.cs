using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Omnicasa.Schedule;

/// <summary>A one-or-more day page hosted inside <see cref="DayAgendaView"/>'s carousel.</summary>
internal class DayAgendaPage : ContentView
{
    private const float MultiDayHeaderHeight = 36f;

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

    private double pinchBase = 60;

    private double pinchScale = 1.0;

    private double pinchAnchorHours = -1;

    private double pinchAnchorViewportY;

    private bool suppressScrollSync;

    /// <summary>Initializes a new instance of the <see cref="DayAgendaPage"/> class.</summary>
    /// <param name="host">The parent <see cref="DayAgendaView"/> that owns shared state.</param>
    public DayAgendaPage(DayAgendaView host)
    {
        this.host = host;

        drawable.Theme = host.Theme;
        drawable.HeaderHeight = HeaderHeightFor(host.DaysPerPage);
        drawable.Scale = new TimeScale((float)host.HourHeight, drawable.HeaderHeight);

        canvas = new GraphicsView
        {
            Drawable = drawable,
            BackgroundColor = Colors.Transparent,
            HeightRequest = drawable.Scale.TotalHeight,
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnCanvasTapped;
        canvas.GestureRecognizers.Add(tap);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        canvas.GestureRecognizers.Add(pinch);

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
        host.SourceChanged += OnSourceChanged;
        host.SharedScrollYChanged += OnSharedScrollYChanged;
        host.DaysPerPageChanged += OnDaysPerPageChanged;
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
            host.SourceChanged -= OnSourceChanged;
            host.SharedScrollYChanged -= OnSharedScrollYChanged;
            host.DaysPerPageChanged -= OnDaysPerPageChanged;
            BindingContextChanged -= OnBindingContextChanged;
        };
    }

    private enum DragKind
    {
        Move,
        Resize,
    }

    private static float HeaderHeightFor(int daysPerPage)
        => daysPerPage > 1 ? MultiDayHeaderHeight : 0f;

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
        pan.PanUpdated += (s, e) => OnHandlePan(kind, e);
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

    private void OnDaysPerPageChanged()
    {
        SyncConfigFromHost();
        ClearSelection();
        _ = ReloadAsync();
    }

    private void SyncConfigFromHost()
    {
        daysInPage = Math.Max(1, host.DaysPerPage);
        var header = HeaderHeightFor(daysInPage);
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

    private void OnSourceChanged(IAppointmentSource? source) => _ = ReloadAsync();

    private async Task ReloadAsync()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        if (anchorDay is null)
        {
            drawable.Days = new[] { DateOnly.FromDateTime(DateTime.Today) };
            drawable.ColumnsByDay = new IReadOnlyList<LaidOutAppointment>[] { Array.Empty<LaidOutAppointment>() };
            drawable.AllDayByDay = new IReadOnlyList<Appointment>[] { Array.Empty<Appointment>() };
            canvas.Invalidate();
            return;
        }

        var n = daysInPage;
        var from = anchorDay.Value;
        var days = new DateOnly[n];
        for (int i = 0; i < n; i++)
        {
            days[i] = DateOnly.FromDateTime(from.AddDays(i));
        }

        if (host.AppointmentSource is null)
        {
            drawable.Days = days;
            drawable.ColumnsByDay = Enumerable.Range(0, n)
                .Select(_ => (IReadOnlyList<LaidOutAppointment>)Array.Empty<LaidOutAppointment>())
                .ToArray();
            drawable.AllDayByDay = Enumerable.Range(0, n)
                .Select(_ => (IReadOnlyList<Appointment>)Array.Empty<Appointment>())
                .ToArray();
            canvas.Invalidate();
            return;
        }

        var to = from.AddDays(n).AddTicks(-1);
        try
        {
            var list = await host.AppointmentSource.GetAsync(from, to, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var columns = new IReadOnlyList<LaidOutAppointment>[n];
            var allDay = new IReadOnlyList<Appointment>[n];
            for (int i = 0; i < n; i++)
            {
                var dayStart = from.AddDays(i);
                var dayEnd = dayStart.AddDays(1);
                var forDay = list.Where(a => a.Start < dayEnd && a.End > dayStart).ToList();
                columns[i] = EventLayoutEngine.Layout(forDay.Where(a => !a.IsAllDay)).ToList();
                allDay[i] = forDay.Where(a => a.IsAllDay).ToList();
            }

            drawable.Days = days;
            drawable.ColumnsByDay = columns;
            drawable.AllDayByDay = allDay;
            drawable.Now = DateTime.Now;
            canvas.Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
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
        if (anchorDay is not null && drawable.ColumnsByDay.Count != daysInPage)
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
                dragging = null;
                canvas.Invalidate();
                PositionHandles();
                break;
        }
    }
}
