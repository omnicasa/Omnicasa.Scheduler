using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>In-memory <see cref="IAppointmentSource"/> used to seed demo data in the sample app.</summary>
public sealed class InMemoryAppointmentSource : IAppointmentSource
{
    private readonly List<Appointment> items = new List<Appointment>();

    /// <summary>Initializes a new instance of the <see cref="InMemoryAppointmentSource"/> class.</summary>
    public InMemoryAppointmentSource()
    {
        Seed();
    }

    /// <inheritdoc />
    public event EventHandler<AppointmentsChangedEventArgs>? Changed;

    /// <summary>Populates the source with reproducible pseudo-random demo appointments.</summary>
    public void Seed()
    {
        var rng = new Random(42);
        var palette = new[]
        {
            Color.FromArgb("#FF3B30"),
            Color.FromArgb("#FF9500"),
            Color.FromArgb("#FFCC00"),
            Color.FromArgb("#34C759"),
            Color.FromArgb("#5AC8FA"),
            Color.FromArgb("#007AFF"),
            Color.FromArgb("#5856D6"),
            Color.FromArgb("#AF52DE"),
            Color.FromArgb("#FF2D55"),
        };
        var titles = new[]
        {
            "Standup", "Design review", "1:1 with Alex", "Lunch", "Gym",
            "Client call", "Sprint planning", "Retro", "Focus time", "Interview",
            "Dentist", "Pickup kids", "Team dinner", "Coffee with Jo", "Doctor",
            "Flight", "Conference", "Workshop", "Deep work", "Code review",
        };

        int[] minuteChoices = new[] { 0, 15, 30, 45 };
        int[] durationChoices = new[] { 30, 45, 60, 90, 120 };
        var today = DateTime.Today;
        for (int d = -90; d <= 180; d++)
        {
            var day = today.AddDays(d);
            int count = rng.Next(0, 5);
            for (int i = 0; i < count; i++)
            {
                int startHour = rng.Next(7, 19);
                int startMinute = minuteChoices[rng.Next(minuteChoices.Length)];
                int durMinutes = durationChoices[rng.Next(durationChoices.Length)];
                var start = day.AddHours(startHour).AddMinutes(startMinute);
                items.Add(new Appointment
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = titles[rng.Next(titles.Length)],
                    Start = start,
                    End = start.AddMinutes(durMinutes),
                    Color = palette[rng.Next(palette.Length)],
                });
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Appointment>> GetAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        IReadOnlyList<Appointment> list = items
            .Where(a => a.End >= from && a.Start <= to)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>Adds an appointment and notifies listeners.</summary>
    /// <param name="a">Appointment to add.</param>
    public void Add(Appointment a)
    {
        items.Add(a);
        Changed?.Invoke(this, new AppointmentsChangedEventArgs());
    }

    /// <summary>Removes an appointment and notifies listeners.</summary>
    /// <param name="a">Appointment to remove.</param>
    public void Remove(Appointment a)
    {
        items.Remove(a);
        Changed?.Invoke(this, new AppointmentsChangedEventArgs());
    }
}
