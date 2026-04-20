namespace Omnicasa.Schedule.Sample;

/// <summary>Root <see cref="Application"/> for the sample app.</summary>
public partial class App : Application
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new AppShell());
}
