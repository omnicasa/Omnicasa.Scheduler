#if ANDROID || IOS
#if IOS
global using ScheduleCanvas = Omnicasa.Schedule.CpuScheduleCanvas;
global using SchedulePaintArgs = SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs;
#else
global using ScheduleCanvas = SkiaSharp.Views.Maui.Controls.SKGLView;
global using SchedulePaintArgs = SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs;
#endif

#if IOS
using CoreAnimation;
using Foundation;
using SkiaSharp.Views.Maui.Controls;

namespace Omnicasa.Schedule;

/// <summary>
/// iOS canvas for the infinite views. MOBILE-12544 SKGLView crashed natively while building its GL
/// context on the first draw, so iOS draws on the CPU; <see cref="HasRenderLoop"/> mirrors SKGLView's.
/// </summary>
internal sealed class CpuScheduleCanvas : SKCanvasView
{
    private CADisplayLink? displayLink;
    private bool hasRenderLoop;

    /// <summary>Initializes a new instance of the <see cref="CpuScheduleCanvas"/> class.</summary>
    public CpuScheduleCanvas()
    {
        // A display link keeps its target alive, so it must not outlive the native view
        HandlerChanged += (_, _) => UpdateDisplayLink();
    }

    /// <summary>Gets or sets a value indicating whether the canvas repaints every display frame.</summary>
    public bool HasRenderLoop
    {
        get => hasRenderLoop;
        set
        {
            if (hasRenderLoop == value)
            {
                return;
            }

            hasRenderLoop = value;
            UpdateDisplayLink();
        }
    }

    private void UpdateDisplayLink()
    {
        if (hasRenderLoop && Handler != null)
        {
            if (displayLink == null)
            {
                displayLink = CADisplayLink.Create(InvalidateSurface);
                displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
            }

            return;
        }

        displayLink?.Invalidate();
        displayLink?.Dispose();
        displayLink = null;
    }
}
#endif
#endif
