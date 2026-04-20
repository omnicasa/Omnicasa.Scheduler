using UIKit;

namespace Omnicasa.Schedule.Sample;

/// <summary>iOS native entry point.</summary>
public static class Program
{
    /// <summary>Native entry point for the iOS application.</summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
