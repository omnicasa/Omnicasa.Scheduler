#if ANDROID || IOS
using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Omnicasa.Schedule;

/// <summary>
/// A single-canvas, continuously scrolling month calendar: full-size months stacked vertically (one
/// month per screen, a "MMMM yyyy" title over a full month grid). The GPU-backed counterpart of
/// <c>MonthCalendarView</c> — instead of composing dozens of MAUI <c>MonthGraphicsView</c> instances
/// in a <c>ScrollView</c>, it draws only the visible months into one <see cref="SKGLView"/> and owns
/// a virtual vertical offset, so a wide year range no longer inflates the view tree. Reports day taps
/// via <see cref="DayTapped"/>. API-compatible with <c>MonthCalendarView</c> so clients migrate by
/// swapping the type. Platform builds only (SKGLView GL is unavailable on the headless net9.0 target).
/// </summary>
public sealed class InfiniteMonthCalendarView : ContentView
{
    // Momentum tunables mirror InfiniteScheduleView's defaults so the fling feel matches the schedule.
    private const double FlingGain = 1.8;

    private const double FlingDecelerationTime = 0.5;

    private const double MaxFlingSpeed = 12000;

    private const double FlingStopSpeed = 30;

    private const double SettleTau = 0.09;

    private const double PanLockThreshold = 10;

    private const double VelocitySmoothing = 0.5;

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleTheme),
            typeof(InfiniteMonthCalendarView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteMonthCalendarView)b).Invalidate());

    /// <summary>Bindable property for <see cref="AppointmentSource"/>.</summary>
    public static readonly BindableProperty AppointmentSourceProperty =
        BindableProperty.Create(
            nameof(AppointmentSource),
            typeof(IAppointmentSource),
            typeof(InfiniteMonthCalendarView),
            null,
            propertyChanged: (b, o, n) => ((InfiniteMonthCalendarView)b).OnSourceChanged((IAppointmentSource?)o, (IAppointmentSource?)n));

    /// <summary>Bindable property for <see cref="MinYear"/>.</summary>
    public static readonly BindableProperty MinYearProperty =
        BindableProperty.Create(
            nameof(MinYear),
            typeof(int),
            typeof(InfiniteMonthCalendarView),
            DateTime.Today.Year - 5,
            propertyChanged: (b, _, _) => ((InfiniteMonthCalendarView)b).OnRangeChanged());

    /// <summary>Bindable property for <see cref="MaxYear"/>.</summary>
    public static readonly BindableProperty MaxYearProperty =
        BindableProperty.Create(
            nameof(MaxYear),
            typeof(int),
            typeof(InfiniteMonthCalendarView),
            DateTime.Today.Year + 5,
            propertyChanged: (b, _, _) => ((InfiniteMonthCalendarView)b).OnRangeChanged());

    /// <summary>Bindable property for <see cref="InitialDate"/>.</summary>
    public static readonly BindableProperty InitialDateProperty =
        BindableProperty.Create(
            nameof(InitialDate),
            typeof(DateOnly),
            typeof(InfiniteMonthCalendarView),
            DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(InfiniteMonthRenderer),
            typeof(InfiniteMonthCalendarView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteMonthCalendarView)b).Invalidate());

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly SKGLView canvas;

    private readonly Stopwatch clock = Stopwatch.StartNew();

    private readonly Dictionary<long, Point> touches = new Dictionary<long, Point>();

    private readonly Dictionary<DateOnly, int> counts = new Dictionary<DateOnly, int>();

    private readonly List<(DateOnly Date, SKRect Rect)> hitMap = new List<(DateOnly, SKRect)>();

    private readonly DayOfWeek firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    private CancellationTokenSource? loadCts;

    private double offsetY = double.NaN;

    private float blockHeight;

    private int blockCount;

    private float touchScale = 1;

    private int? pendingScrollIndex;

    private Point pressPoint;

    private bool pressValid;

    private bool panning;

    private bool flinging;

    private bool settling;

    private bool caughtMotion;

    private bool multiTouch;

    private long primaryTouchId;

    private int lockAxis;

    private double velocityY;

    private double settleTarget;

    private double panStartOffset;

    private double lastPanTotalY;

    private double panSampleSeconds;

    private double lastFrameSeconds;

    /// <summary>Initializes a new instance of the <see cref="InfiniteMonthCalendarView"/> class.</summary>
    public InfiniteMonthCalendarView()
    {
        canvas = new SKGLView { HasRenderLoop = false, EnableTouchEvents = true };
        canvas.PaintSurface += OnPaintSurface;
        canvas.Touch += OnTouch;
        Content = canvas;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Occurs when the user taps a day cell inside any month.</summary>
    public event EventHandler<DayTappedEventArgs>? DayTapped;

    /// <summary>Gets or sets the color theme used while rendering.</summary>
    public ScheduleTheme Theme
    {
        get => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Gets or sets the appointment source used to compute event-density dots.</summary>
    public IAppointmentSource? AppointmentSource
    {
        get => (IAppointmentSource?)GetValue(AppointmentSourceProperty);
        set => SetValue(AppointmentSourceProperty, value);
    }

    /// <summary>Gets or sets the earliest year rendered (inclusive).</summary>
    public int MinYear
    {
        get => (int)GetValue(MinYearProperty);
        set => SetValue(MinYearProperty, value);
    }

    /// <summary>Gets or sets the latest year rendered (inclusive).</summary>
    public int MaxYear
    {
        get => (int)GetValue(MaxYearProperty);
        set => SetValue(MaxYearProperty, value);
    }

    /// <summary>Gets or sets the month initially scrolled into view on first load.</summary>
    public DateOnly InitialDate
    {
        get => (DateOnly)GetValue(InitialDateProperty);
        set => SetValue(InitialDateProperty, value);
    }

    /// <summary>Gets or sets the painter for each month grid; defaults to the built-in look.</summary>
    public InfiniteMonthRenderer Renderer
    {
        get => (InfiniteMonthRenderer)GetValue(RendererProperty) ?? InfiniteMonthRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    private ScheduleTheme ActiveTheme => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;

    private InfiniteMonthRenderer ActiveRenderer => Renderer;

    /// <summary>Scrolls so the given month sits at the top of the viewport.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="month">Month number 1–12.</param>
    /// <param name="animated">Reserved for parity; the scroll is currently immediate.</param>
    public void ScrollToMonth(int year, int month, bool animated = true)
    {
        _ = animated;
        int idx = ClampBlockIndex(MonthGridGeometry.BlockIndex(year, month, MinYear));
        if (blockHeight > 0 && blockCount > 0)
        {
            StopMotion();
            offsetY = ClampOffset(idx * (double)blockHeight);
            Invalidate();
        }
        else
        {
            pendingScrollIndex = idx;
        }
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

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        // Block height tracks the viewport, so a resize/rotation invalidates the px offset. Remember
        // the top month and reseed to it once the new height is known.
        if (!double.IsNaN(offsetY) && blockHeight > 0)
        {
            pendingScrollIndex = ClampBlockIndex((int)Math.Round(offsetY / blockHeight));
        }

        offsetY = double.NaN;
        Invalidate();
    }

    private void OnRangeChanged()
    {
        // Block indices are relative to MinYear; a range change reseeds to InitialDate (like a rebuild).
        offsetY = double.NaN;
        pendingScrollIndex = null;
        _ = RefreshCountsAsync();
        Invalidate();
    }

    private int ClampBlockIndex(int index)
    {
        int total = Math.Max(1, (MaxYear - MinYear + 1) * 12);
        return Math.Clamp(index, 0, total - 1);
    }

    private double ClampOffset(double value)
        => MonthGridGeometry.ClampOffset(value, blockHeight, blockHeight, blockCount);

    private void Invalidate() => canvas.InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var skCanvas = e.Surface.Canvas;
        var theme = ActiveTheme;

        float logicalW = (float)canvas.Width;
        float logicalH = (float)canvas.Height;
        if (logicalW <= 0 || logicalH <= 0)
        {
            skCanvas.Clear(ToSk(theme.Background));
            return;
        }

        if (flinging)
        {
            StepFling();
        }

        float scale = e.BackendRenderTarget.Width / logicalW;
        touchScale = scale; // authoritative px→logical ratio for touch coords
        skCanvas.Scale(scale);

        blockHeight = logicalH;
        blockCount = Math.Max(0, (MaxYear - MinYear + 1) * 12);

        if (double.IsNaN(offsetY))
        {
            int seedIndex = pendingScrollIndex ?? MonthGridGeometry.BlockIndex(InitialDate.Year, InitialDate.Month, MinYear);
            pendingScrollIndex = null;
            offsetY = ClampOffset(ClampBlockIndex(seedIndex) * (double)blockHeight);
        }

        skCanvas.Clear(ToSk(theme.Background));
        hitMap.Clear();

        int first = MonthGridGeometry.FirstVisibleBlock(offsetY, blockHeight);
        int count = MonthGridGeometry.VisibleBlockCount(logicalH, blockHeight);
        for (int i = first; i < first + count; i++)
        {
            if (i < 0 || i >= blockCount)
            {
                continue;
            }

            int year = MinYear + (i / 12);
            int month = (i % 12) + 1;
            float top = (float)((i * (double)blockHeight) - offsetY);
            DrawMonthBlock(skCanvas, new SKRect(0, top, logicalW, top + blockHeight), year, month, theme);
        }

        // Drop back to on-demand painting when idle so the GPU isn't kept busy.
        if (!flinging && !panning && canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = false;
        }
    }

    private void DrawMonthBlock(SKCanvas skCanvas, SKRect rect, int year, int month, ScheduleTheme theme)
    {
        var renderer = ActiveRenderer;
        const float pad = 16f;
        float contentLeft = rect.Left + pad;
        float contentW = rect.Width - (pad * 2);

        float titleSize = (float)(theme.MonthHeaderFontSize ?? 22);
        float titleTop = rect.Top + 12;
        float titleH = titleSize + 6;
        renderer.DrawTitle(new MonthTitlePaintContext
        {
            Canvas = skCanvas,
            Theme = theme,
            Rect = new SKRect(contentLeft + 2, titleTop, contentLeft + contentW, titleTop + titleH),
            Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            FontSize = titleSize,
        });

        float gridTop = titleTop + titleH + 4;
        float gridH = rect.Bottom - gridTop;
        if (gridH <= 0)
        {
            return;
        }

        float cellW = contentW / 7f;
        float rowH = gridH / 7f; // one weekday row + six day rows

        float weekdaySize = (float)(theme.WeekdayFontSize ?? Math.Min(15f, rowH * 0.32f));
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow];
            var letter = string.IsNullOrEmpty(name) ? "?" : name[..1];
            renderer.DrawWeekday(new MonthWeekdayPaintContext
            {
                Canvas = skCanvas,
                Theme = theme,
                Rect = new SKRect(contentLeft + (i * cellW), gridTop, contentLeft + ((i + 1) * cellW), gridTop + rowH),
                Text = letter,
                DayOfWeek = dow,
                FontSize = weekdaySize,
            });
        }

        float daySize = (float)(theme.DayNumberFontSize ?? Math.Min(22f, rowH * 0.42f));
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var dc in MonthGridGeometry.DayCells(contentLeft, gridTop, contentW, gridH, year, month, firstDayOfWeek))
        {
            var cell = new SKRect(dc.X, dc.Y, dc.X + dc.Width, dc.Y + dc.Height);
            var date = dc.Date;
            hitMap.Add((date, cell));

            bool isToday = date == today;
            SKColor textColor;
            if (isToday)
            {
                textColor = SKColors.White;
            }
            else if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                textColor = ToSk(theme.Saturday);
            }
            else if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                textColor = ToSk(theme.Sunday);
            }
            else
            {
                textColor = ToSk(theme.Foreground);
            }

            renderer.DrawDay(new MonthDayPaintContext
            {
                Canvas = skCanvas,
                Theme = theme,
                Rect = cell,
                Date = date,
                IsToday = isToday,
                EventCount = GetCount(date),
                TextColor = textColor,
                FontSize = daySize,
                Compact = false,
            });
        }
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;
        var p = LogicalPoint(e.Location);
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                touches[e.Id] = p;
                if (touches.Count >= 2)
                {
                    // A second finger (e.g. a pinch) suspends scrolling so the two fingers'
                    // interleaved move events can't fight over the offset and jitter the view.
                    EnterMultiTouch();
                }
                else
                {
                    primaryTouchId = e.Id;
                    OnTouchPressed(p);
                }

                break;

            case SKTouchAction.Moved:
                if (touches.ContainsKey(e.Id))
                {
                    touches[e.Id] = p;
                }

                // Only the primary finger drives the scroll; ignore others (and all moves while suspended).
                if (!multiTouch && e.Id == primaryTouchId)
                {
                    OnTouchMoved(p);
                }

                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                touches.Remove(e.Id);
                if (multiTouch)
                {
                    // Stay suspended until every finger lifts, then settle onto a month boundary.
                    if (touches.Count == 0)
                    {
                        ExitMultiTouch();
                    }
                }
                else if (e.Id == primaryTouchId)
                {
                    OnTouchReleased(e.ActionType != SKTouchAction.Released);
                }

                break;
        }
    }

    private void EnterMultiTouch()
    {
        multiTouch = true;
        panning = false;
        pressValid = false;
        lockAxis = 0;
        StopMotion();
    }

    private void ExitMultiTouch()
    {
        multiTouch = false;
        SettleToBlock();
    }

    private void OnTouchPressed(Point p)
    {
        // Pressing while a fling runs "catches" it: stop, but remember so the release doesn't also
        // fire a day tap (matches native list behavior).
        caughtMotion = flinging;
        StopMotion();
        pressPoint = p;
        pressValid = true;
        lockAxis = 0;
        velocityY = 0;
        panning = true;
        panStartOffset = offsetY;
        lastPanTotalY = 0;
        panSampleSeconds = clock.Elapsed.TotalSeconds;
    }

    private void OnTouchMoved(Point p)
    {
        if (!panning)
        {
            return;
        }

        double totalY = p.Y - pressPoint.Y;

        // Commit to a scroll once the finger clearly moves (below threshold stays a candidate tap).
        if (lockAxis == 0)
        {
            if (Math.Abs(totalY) < PanLockThreshold)
            {
                return;
            }

            lockAxis = 1;
            pressValid = false;
            BeginRenderLoop();
        }

        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Max(now - panSampleSeconds, 1e-3);

        // EMA keeps the flick's peak velocity (the last pre-lift sample is usually decelerating).
        double instY = (totalY - lastPanTotalY) / dt;
        velocityY = (velocityY * (1 - VelocitySmoothing)) + (instY * VelocitySmoothing);
        offsetY = ClampOffset(panStartOffset - totalY);

        lastPanTotalY = totalY;
        panSampleSeconds = now;
        Invalidate();
    }

    private void OnTouchReleased(bool cancelled)
    {
        panning = false;

        if (lockAxis != 0)
        {
            StartFling();
        }
        else
        {
            if (pressValid && !caughtMotion && !cancelled)
            {
                DispatchTap(pressPoint);
            }

            SettleToBlock();
        }

        caughtMotion = false;
    }

    private void DispatchTap(Point p)
    {
        var hit = (float)p.X;
        var hy = (float)p.Y;
        foreach (var (date, rect) in hitMap)
        {
            if (rect.Contains(hit, hy))
            {
                DayTapped?.Invoke(this, new DayTappedEventArgs(date));
                return;
            }
        }
    }

    private void StartFling()
    {
        // Content moves opposite the finger; gain punches up the flick, then clamp the extremes.
        velocityY = Math.Clamp(-velocityY * FlingGain, -MaxFlingSpeed, MaxFlingSpeed);
        flinging = true;
        settling = false;
        lastFrameSeconds = clock.Elapsed.TotalSeconds;
        BeginRenderLoop();
    }

    // Advances momentum once per rendered frame (driven from OnPaintSurface), so motion is
    // vsync-paced rather than tied to throttled gesture callbacks.
    private void StepFling()
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - lastFrameSeconds, 1e-3, 0.05);
        lastFrameSeconds = now;

        if (!settling)
        {
            offsetY = ClampOffset(offsetY + (velocityY * dt));

            // Frame-rate-independent exponential decay.
            double decay = Math.Exp(-dt / FlingDecelerationTime);
            velocityY *= decay;

            if (Math.Abs(velocityY) < FlingStopSpeed)
            {
                settling = true;
                settleTarget = ClampOffset(MonthGridGeometry.SnapToBlock(offsetY, blockHeight));
            }
        }
        else
        {
            double k = 1 - Math.Exp(-dt / SettleTau);
            offsetY += (settleTarget - offsetY) * k;
            if (Math.Abs(settleTarget - offsetY) < 0.5)
            {
                offsetY = settleTarget;
                StopMotion();
            }
        }
    }

    // Eases the offset onto the nearest whole month, then goes idle. No-op (just stops) when aligned.
    private void SettleToBlock()
    {
        settleTarget = ClampOffset(MonthGridGeometry.SnapToBlock(offsetY, blockHeight));
        if (Math.Abs(settleTarget - offsetY) < 0.5)
        {
            offsetY = settleTarget;
            StopMotion();
            return;
        }

        velocityY = 0;
        flinging = true;
        settling = true;
        lastFrameSeconds = clock.Elapsed.TotalSeconds;
        BeginRenderLoop();
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
        velocityY = 0;
        if (!panning && canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = false;
        }
    }

    private Point LogicalPoint(SKPoint px)
    {
        // SKGLView touch Location is in device pixels; convert with the same ratio the paint uses.
        double sc = touchScale > 0
            ? touchScale
            : (canvas.Width > 0 ? canvas.CanvasSize.Width / canvas.Width : 1.0);
        if (sc <= 0)
        {
            sc = 1.0;
        }

        return new Point(px.X / sc, px.Y / sc);
    }

    private int GetCount(DateOnly d) => counts.TryGetValue(d, out var c) ? c : 0;

    private async Task RefreshCountsAsync()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        if (AppointmentSource is null)
        {
            counts.Clear();
            Invalidate();
            return;
        }

        try
        {
            var from = new DateTime(MinYear, 1, 1);
            var to = new DateTime(MaxYear, 12, 31, 23, 59, 59);
            var list = await AppointmentSource.GetAsync(from, to, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            counts.Clear();
            foreach (var a in list)
            {
                var d0 = DateOnly.FromDateTime(a.Start);
                var d1 = DateOnly.FromDateTime(a.End);
                if (d1 < d0)
                {
                    d1 = d0;
                }

                for (var d = d0; d <= d1; d = d.AddDays(1))
                {
                    counts[d] = counts.TryGetValue(d, out var c) ? c + 1 : 1;
                }
            }

            Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnSourceChanged(IAppointmentSource? oldSrc, IAppointmentSource? newSrc)
    {
        if (oldSrc is not null)
        {
            oldSrc.Changed -= OnSourceDataChanged;
        }

        if (newSrc is not null)
        {
            newSrc.Changed += OnSourceDataChanged;
        }

        _ = RefreshCountsAsync();
    }

    private void OnSourceDataChanged(object? sender, AppointmentsChangedEventArgs e) => _ = RefreshCountsAsync();
}
#endif
