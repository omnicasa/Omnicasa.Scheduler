using Microsoft.Extensions.Logging;

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
            .UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
