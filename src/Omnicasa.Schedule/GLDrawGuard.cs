#if ANDROID || IOS
using SkiaSharp.Views.Maui.Controls;

namespace Omnicasa.Schedule;

/// <summary>
/// iOS guard for <see cref="SKGLView"/>. SkiaSharp builds its GL context lazily on the first draw; when
/// no GL context is current at that moment Skia reads null GL strings and crashes natively (MOBILE-12544).
/// The guard skips that frame and asks for another one shortly after. No-op on other targets.
/// </summary>
internal static class GLDrawGuard
{
    /// <summary>Wraps the native view's draw delegate once the handler is attached.</summary>
    public static void Attach(SKGLView view)
    {
#if IOS
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is SkiaSharp.Views.iOS.SKGLView platformView)
            {
                IosGuard.Wrap(platformView);
            }
        };
#endif
    }

#if IOS
#pragma warning disable CA1422 // GLKit and OpenGLES are deprecated on iOS but still back SKGLView
    private static class IosGuard
    {
        // Roots each guard for the lifetime of its native view; GLKView only holds its delegate weakly.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GLKit.GLKView, GuardDelegate> Guards = new System.Runtime.CompilerServices.ConditionalWeakTable<GLKit.GLKView, GuardDelegate>();

        public static void Wrap(SkiaSharp.Views.iOS.SKGLView platformView)
        {
            if (Guards.TryGetValue(platformView, out _))
            {
                return;
            }

            var guard = new GuardDelegate(platformView);
            Guards.Add(platformView, guard);
            platformView.Delegate = guard;
        }

        private sealed class GuardDelegate : GLKit.GLKViewDelegate
        {
            private const uint GlVersion = 0x1F02;

            private readonly WeakReference<SkiaSharp.Views.iOS.SKGLView> target;

            public GuardDelegate(SkiaSharp.Views.iOS.SKGLView target)
            {
                this.target = new WeakReference<SkiaSharp.Views.iOS.SKGLView>(target);
            }

            [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/OpenGLES.framework/OpenGLES", EntryPoint = "glGetString")]
            private static extern IntPtr GlGetString(uint name);

            public override void DrawInRect(GLKit.GLKView view, CoreGraphics.CGRect rect)
            {
                if (!target.TryGetTarget(out var skiaView))
                {
                    return;
                }

                // Once the GRContext exists GL was usable, so only the first draws need the check.
                if (skiaView.GRContext == null
                    && (OpenGLES.EAGLContext.CurrentContext == null || GlGetString(GlVersion) == IntPtr.Zero))
                {
                    ScheduleDiagnostics.Log("Skipped GL draw: no current GL context");
                    CoreFoundation.DispatchQueue.MainQueue.DispatchAfter(
                        new CoreFoundation.DispatchTime(CoreFoundation.DispatchTime.Now, TimeSpan.FromMilliseconds(250)),
                        view.SetNeedsDisplay);
                    return;
                }

                skiaView.DrawInRect(view, rect);
            }
        }
    }
#pragma warning restore CA1422
#endif
}
#endif
