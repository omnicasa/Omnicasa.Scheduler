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
/// A single-canvas, continuously scrolling year-at-a-glance calendar: one year per screen (a big
/// "yyyy" header over a 4×3 grid of compact month tiles with event-density dots). The GPU-backed
/// counterpart of <c>YearCalendarView</c> — instead of composing 12 MAUI <c>MonthGraphicsView</c>
/// instances per year in a <c>ScrollView</c>, it draws only the visible years into one
/// <see cref="SKGLView"/> and owns a virtual vertical offset, so a wide year range no longer inflates
/// the view tree. Reports day taps via <see cref="DayTapped"/>. API-compatible with
/// <c>YearCalendarView</c> so clients migrate by swapping the type. Platform builds only.
/// </summary>
public sealed class InfiniteYearCalendarView : ContentView
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
            typeof(InfiniteYearCalendarView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteYearCalendarView)b).Invalidate());

    /// <summary>Bindable property for <see cref="AppointmentSource"/>.</summary>
    public static readonly BindableProperty AppointmentSourceProperty =
        BindableProperty.Create(
            nameof(AppointmentSource),
            typeof(IAppointmentSource),
            typeof(InfiniteYearCalendarView),
            null,
            propertyChanged: (b, o, n) => ((InfiniteYearCalendarView)b).OnSourceChanged((IAppointmentSource?)o, (IAppointmentSource?)n));

    /// <summary>Bindable property for <see cref="MinYear"/>.</summary>
    public static readonly BindableProperty MinYearProperty =
        BindableProperty.Create(
            nameof(MinYear),
            typeof(int),
            typeof(InfiniteYearCalendarView),
            DateTime.Today.Year - 5,
            propertyChanged: (b, _, _) => ((InfiniteYearCalendarView)b).OnRangeChanged());

    /// <summary>Bindable property for <see cref="MaxYear"/>.</summary>
    public static readonly BindableProperty MaxYearProperty =
        BindableProperty.Create(
            nameof(MaxYear),
            typeof(int),
            typeof(InfiniteYearCalendarView),
            DateTime.Today.Year + 5,
            propertyChanged: (b, _, _) => ((InfiniteYearCalendarView)b).OnRangeChanged());

    /// <summary>Bindable property for <see cref="InitialYear"/>.</summary>
    public static readonly BindableProperty InitialYearProperty =
        BindableProperty.Create(
            nameof(InitialYear),
            typeof(int),
            typeof(InfiniteYearCalendarView),
            DateTime.Today.Year);

    /// <summary>Bindable property for <see cref="Renderer"/>.</summary>
    public static readonly BindableProperty RendererProperty =
        BindableProperty.Create(
            nameof(Renderer),
            typeof(InfiniteMonthRenderer),
            typeof(InfiniteYearCalendarView),
            null,
            propertyChanged: (b, _, _) => ((InfiniteYearCalendarView)b).Invalidate());

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

    /// <summary>Initializes a new instance of the <see cref="InfiniteYearCalendarView"/> class.</summary>
    public InfiniteYearCalendarView()
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

    /// <summary>Gets or sets the year initially scrolled into view on first load.</summary>
    public int InitialYear
    {
        get => (int)GetValue(InitialYearProperty);
        set => SetValue(InitialYearProperty, value);
    }

    /// <summary>Gets or sets the painter for each month tile; defaults to the built-in look.</summary>
    public InfiniteMonthRenderer Renderer
    {
        get => (InfiniteMonthRenderer)GetValue(RendererProperty) ?? InfiniteMonthRenderer.Default;
        set => SetValue(RendererProperty, value);
    }

    private ScheduleTheme ActiveTheme => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;

    private InfiniteMonthRenderer ActiveRenderer => Renderer;

    /// <summary>Scrolls so the given year sits at the top of the viewport.</summary>
    /// <param name="year">Four-digit year.</param>
    /// <param name="animated">Reserved for parity; the scroll is currently immediate.</param>
    public void ScrollToYear(int year, bool animated = true)
    {
        _ = animated;
        int idx = ClampBlockIndex(year - MinYear);
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
        // the top year and reseed to it once the new height is known.
        if (!double.IsNaN(offsetY) && blockHeight > 0)
        {
            pendingScrollIndex = ClampBlockIndex((int)Math.Round(offsetY / blockHeight));
        }

        offsetY = double.NaN;
        Invalidate();
    }

    private void OnRangeChanged()
    {
        // Block indices are relative to MinYear; a range change reseeds to InitialYear (like a rebuild).
        offsetY = double.NaN;
        pendingScrollIndex = null;
        _ = RefreshCountsAsync();
        Invalidate();
    }

    private int ClampBlockIndex(int index)
    {
        int total = Math.Max(1, MaxYear - MinYear + 1);
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
        blockCount = Math.Max(0, MaxYear - MinYear + 1);

        if (double.IsNaN(offsetY))
        {
            int seedIndex = pendingScrollIndex ?? (InitialYear - MinYear);
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

            int year = MinYear + i;
            float top = (float)((i * (double)blockHeight) - offsetY);
            DrawYearBlock(skCanvas, new SKRect(0, top, logicalW, top + blockHeight), year, theme);
        }

        // Drop back to on-demand painting when idle so the GPU isn't kept busy.
        if (!flinging && !panning && canvas.HasRenderLoop)
        {
            canvas.HasRenderLoop = false;
        }
    }

    private void DrawYearBlock(SKCanvas skCanvas, SKRect rect, int year, ScheduleTheme theme)
    {
        const float pad = 16f;
        const float colSpacing = 10f;
        const float rowSpacing = 10f;
        float contentLeft = rect.Left + pad;
        float contentW = rect.Width - (pad * 2);

        // Year header.
        float headerSize = 34f;
        float headerTop = rect.Top + 8;
        float headerH = headerSize + 8;
        ActiveRenderer.DrawTitle(new MonthTitlePaintContext
        {
            Canvas = skCanvas,
            Theme = theme,
            Rect = new SKRect(contentLeft, headerTop, contentLeft + contentW, headerTop + headerH),
            Text = year.ToString(CultureInfo.CurrentCulture),
            FontSize = headerSize,
        });

        // 4 columns × 3 rows of compact month tiles filling the rest.
        float gridTop = headerTop + headerH + 8;
        float gridH = rect.Bottom - gridTop - 8;
        if (gridH <= 0 || contentW <= 0)
        {
            return;
        }

        float tileW = (contentW - (3 * colSpacing)) / 4f;
        float tileH = (gridH - (2 * rowSpacing)) / 3f;
        if (tileW <= 0 || tileH <= 0)
        {
            return;
        }

        for (int m = 1; m <= 12; m++)
        {
            int idx = m - 1;
            int row = idx / 4;
            int col = idx % 4;
            float tx = contentLeft + (col * (tileW + colSpacing));
            float ty = gridTop + (row * (tileH + rowSpacing));
            DrawMonthTile(skCanvas, new SKRect(tx, ty, tx + tileW, ty + tileH), year, m, theme);
        }
    }

    // Compact month tile: an abbreviated "MMM" header, a weekday-letter row, then the day grid.
    // Sizing mirrors MonthDrawable's compact path so the look matches the old YearCalendarView.
    private void DrawMonthTile(SKCanvas skCanvas, SKRect rect, int year, int month, ScheduleTheme theme)
    {
        var renderer = ActiveRenderer;
        float w = rect.Width;
        float h = rect.Height;

        float headerH = Math.Min(22f, h * 0.14f);
        float headerSize = (float)(theme.MonthHeaderFontSize ?? (headerH * 0.8f));
        renderer.DrawTitle(new MonthTitlePaintContext
        {
            Canvas = skCanvas,
            Theme = theme,
            Rect = new SKRect(rect.Left + 2, rect.Top, rect.Right, rect.Top + headerH),
            Text = new DateTime(year, month, 1).ToString("MMM", CultureInfo.CurrentCulture),
            FontSize = headerSize,
        });

        float gridTop = rect.Top + headerH + 2;
        float cellW = w / 7f;
        float rowH = (rect.Bottom - gridTop) / 7f;
        if (rowH <= 0)
        {
            return;
        }

        float weekdaySize = (float)(theme.WeekdayFontSize ?? Math.Min(9f, rowH * 0.38f));
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow];
            var letter = string.IsNullOrEmpty(name) ? "?" : name[..1];
            renderer.DrawWeekday(new MonthWeekdayPaintContext
            {
                Canvas = skCanvas,
                Theme = theme,
                Rect = new SKRect(rect.Left + (i * cellW), gridTop, rect.Left + ((i + 1) * cellW), gridTop + rowH),
                Text = letter,
                DayOfWeek = dow,
                FontSize = weekdaySize,
            });
        }

        float daySize = (float)(theme.DayNumberFontSize ?? Math.Min(12f, rowH * 0.48f));
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var dc in MonthGridGeometry.DayCells(rect.Left, gridTop, w, rect.Bottom - gridTop, year, month, firstDayOfWeek))
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
                Compact = true,
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
                    // Stay suspended until every finger lifts, then settle onto a year boundary.
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
        var hx = (float)p.X;
        var hy = (float)p.Y;
        foreach (var (date, rect) in hitMap)
        {
            if (rect.Contains(hx, hy))
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

    // Eases the offset onto the nearest whole year, then goes idle. No-op (just stops) when aligned.
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
