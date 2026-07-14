using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Controls.Hosting;
using SkiaSharp.Views.Maui.Handlers;

namespace Omnicasa.Schedule.Sample;

/// <summary>Entry point that configures the MAUI host builder.</summary>
public static class MauiProgram
{
    /// <summary>Builds and returns the configured <see cref="MauiApp"/>.</summary>
    /// <returns>The constructed MAUI application.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureMauiHandlers(handlers =>
            {
                // Explicit so the SKGLView handler is guaranteed registered on every platform.
                handlers.AddHandler(typeof(SKGLView), typeof(SKGLViewHandler));
                handlers.AddHandler(typeof(SKCanvasView), typeof(SKCanvasViewHandler));
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
