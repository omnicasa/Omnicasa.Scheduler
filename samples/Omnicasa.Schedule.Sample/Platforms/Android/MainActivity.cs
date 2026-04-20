using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Omnicasa.Schedule.Sample;

/// <summary>Android launcher activity.</summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = MainActivity.HandledConfigChanges)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>The set of configuration changes the activity handles itself.</summary>
    internal const ConfigChanges HandledConfigChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density;
}
