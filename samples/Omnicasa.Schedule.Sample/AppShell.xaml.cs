namespace Omnicasa.Schedule.Sample;

/// <summary>Shell hosting the pages of the sample app.</summary>
public partial class AppShell : Shell
{
    /// <summary>Initializes a new instance of the <see cref="AppShell"/> class.</summary>
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(DayPage), typeof(DayPage));
        Routing.RegisterRoute(nameof(SchedulePage), typeof(SchedulePage));
    }
}
