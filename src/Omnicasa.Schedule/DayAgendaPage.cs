using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Omnicasa.Schedule;

/// <summary>A single-day page hosted inside <see cref="DayAgendaView"/>'s carousel.</summary>
internal class DayAgendaPage : ContentView
{
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

    private CancellationTokenSource? loadCts;

    private DateTime? currentDay;

    /// <summary>Initializes a new instance of the <see cref="DayAgendaPage"/> class.</summary>
    /// <param name="host">The parent <see cref="DayAgendaView"/> that owns shared state.</param>
    public DayAgendaPage(DayAgendaView host)
    {
        this.host = host;

        drawable.Theme = host.Theme;
        drawable.Scale = new TimeScale((float)host.HourHeight);

        canvas = new GraphicsView
        {
            Drawable = drawable,
            BackgroundColor = Colors.Transparent,
            HeightRequest = drawable.Scale.TotalHeight,
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnCanvasTapped;
        canvas.GestureRecognizers.Add(tap);

        scroll = new ScrollView
        {
            Content = canvas,
            Orientation = ScrollOrientation.Vertical,
        };

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
        BindingContextChanged += OnBindingContextChanged;
        Loaded += (_, _) => ScrollToReasonableHour();
        Unloaded += (_, _) =>
        {
            host.HourHeightChanged -= OnHourHeightChanged;
            host.ThemeChanged -= OnThemeChanged;
            host.SourceChanged -= OnSourceChanged;
            BindingContextChanged -= OnBindingContextChanged;
        };
    }

    private enum DragKind
    {
        Move,
        Resize,
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
        pan.PanUpdated += (s, e) => OnHandlePan(kind, e);
        b.GestureRecognizers.Add(pan);
        return b;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is DateTime d)
        {
            currentDay = d.Date;
            drawable.Day = DateOnly.FromDateTime(d);
            ClearSelection();
            _ = ReloadAsync();
        }
    }

    private void OnHourHeightChanged()
    {
        drawable.Scale = new TimeScale((float)host.HourHeight);
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
        if (currentDay is null || host.AppointmentSource is null)
        {
            drawable.Appointments = Array.Empty<LaidOutAppointment>();
            canvas.Invalidate();
            return;
        }

        var day = currentDay.Value;
        try
        {
            var list = await host.AppointmentSource.GetAsync(day, day.AddDays(1).AddTicks(-1), ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var forDay = list.Where(a => a.Start < day.AddDays(1) && a.End > day).ToList();
            drawable.Appointments = EventLayoutEngine.Layout(forDay).ToList();
            drawable.AllDay = forDay.Where(a => a.IsAllDay).ToList();
            drawable.Now = DateTime.Now;
            canvas.Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ScrollToReasonableHour()
    {
        var hour = Math.Max(0, DateTime.Now.Hour - 1);
        var y = drawable.Scale.YForTime(TimeSpan.FromHours(hour));
        _ = scroll.ScrollToAsync(0, y, false);
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
        if (selected is null || currentDay is null)
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
                    var newStart = ClampToDay(dragOriginStart.AddMinutes(minutes), currentDay.Value, duration);
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

                    var dayEnd = currentDay.Value.AddDays(1);
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
