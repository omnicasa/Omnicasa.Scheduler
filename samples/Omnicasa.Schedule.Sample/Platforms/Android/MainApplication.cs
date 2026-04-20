using Android.App;
using Android.Runtime;

namespace Omnicasa.Schedule.Sample;

/// <summary>Android application bootstrap for MAUI.</summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>Initializes a new instance of the <see cref="MainApplication"/> class.</summary>
    /// <param name="handle">Native handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
