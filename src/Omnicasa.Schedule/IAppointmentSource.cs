namespace Omnicasa.Schedule;

/// <summary>
/// Provides appointments to the schedule controls for a requested date range.
/// </summary>
public interface IAppointmentSource
{
    /// <summary>Occurs when the underlying appointment data has changed and consumers should refresh.</summary>
    event EventHandler<AppointmentsChangedEventArgs>? Changed;

    /// <summary>Fetches appointments overlapping the given range.</summary>
    /// <param name="from">Inclusive range start.</param>
    /// <param name="to">Inclusive range end.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching appointments in unspecified order.</returns>
    Task<IReadOnlyList<Appointment>> GetAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>Payload for <see cref="IAppointmentSource.Changed"/>.</summary>
public sealed class AppointmentsChangedEventArgs : EventArgs
{
    /// <summary>Gets the optional lower bound of the affected range.</summary>
    public DateTime? From { get; init; }

    /// <summary>Gets the optional upper bound of the affected range.</summary>
    public DateTime? To { get; init; }
}

/// <summary>Event payload carrying a single appointment.</summary>
public sealed class AppointmentEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="AppointmentEventArgs"/> class.</summary>
    /// <param name="a">The appointment involved.</param>
    public AppointmentEventArgs(Appointment a)
    {
        Appointment = a;
    }

    /// <summary>Gets the appointment associated with the event.</summary>
    public Appointment Appointment { get; }
}

/// <summary>Event payload raised when a day cell is tapped in the year view.</summary>
public sealed class DayTappedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="DayTappedEventArgs"/> class.</summary>
    /// <param name="d">The tapped date.</param>
    public DayTappedEventArgs(DateOnly d)
    {
        Date = d;
    }

    /// <summary>Gets the tapped date.</summary>
    public DateOnly Date { get; }
}
