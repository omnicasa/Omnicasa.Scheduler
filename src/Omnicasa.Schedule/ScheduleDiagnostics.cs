namespace Omnicasa.Schedule;

/// <summary>
/// Opt-in performance logging for the schedule controls. Off by default; host apps enable it
/// while profiling (<c>ScheduleDiagnostics.Enabled = true</c>) and read the <c>[Schedule]</c>
/// lines from the console: rebuild passes (count, duration, input sizes) and slow canvas draws.
/// </summary>
public static class ScheduleDiagnostics
{
    /// <summary>Master switch; keep off in production.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Body draws faster than this many milliseconds are not logged.</summary>
    public static double SlowDrawMilliseconds { get; set; } = 8;

    /// <summary>Writes a <c>[Schedule]</c> console line when <see cref="Enabled"/>.</summary>
    internal static void Log(string message)
    {
        if (Enabled)
        {
            Console.WriteLine($"[Schedule] {message}");
        }
    }
}
