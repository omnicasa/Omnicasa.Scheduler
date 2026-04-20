namespace Omnicasa.Schedule;

/// <summary>
/// An appointment positioned within an overlapping column group for day-view rendering.
/// </summary>
public sealed class LaidOutAppointment
{
    /// <summary>Initializes a new instance of the <see cref="LaidOutAppointment"/> class.</summary>
    /// <param name="appointment">The underlying appointment.</param>
    /// <param name="column">The zero-based column the appointment occupies within its overlap group.</param>
    /// <param name="columnsInGroup">The total number of columns in the overlap group.</param>
    public LaidOutAppointment(Appointment appointment, int column, int columnsInGroup)
    {
        Appointment = appointment;
        Column = column;
        ColumnsInGroup = columnsInGroup;
    }

    /// <summary>Gets the underlying appointment.</summary>
    public Appointment Appointment { get; }

    /// <summary>Gets the zero-based column the appointment occupies within its overlap group.</summary>
    public int Column { get; }

    /// <summary>Gets the total number of columns in the overlap group.</summary>
    public int ColumnsInGroup { get; }
}

/// <summary>
/// Computes horizontal column positions for overlapping appointments in a single day.
/// </summary>
public static class EventLayoutEngine
{
    /// <summary>
    /// Assigns each non-all-day appointment to a column within its overlap group.
    /// </summary>
    /// <param name="items">Appointments that occur on the same day.</param>
    /// <returns>A list of laid-out appointments containing column assignments.</returns>
    public static IReadOnlyList<LaidOutAppointment> Layout(IEnumerable<Appointment> items)
    {
        var sorted = items
            .Where(a => !a.IsAllDay)
            .OrderBy(a => a.Start)
            .ThenBy(a => a.End)
            .ToList();

        var result = new List<LaidOutAppointment>(sorted.Count);
        var group = new List<(Appointment Item, int Column)>();
        DateTime? groupEnd = null;

        void Flush()
        {
            if (group.Count == 0)
            {
                return;
            }

            int cols = group.Max(t => t.Column) + 1;
            foreach (var (item, col) in group)
            {
                result.Add(new LaidOutAppointment(item, col, cols));
            }

            group.Clear();
            groupEnd = null;
        }

        foreach (var ev in sorted)
        {
            if (groupEnd is null || ev.Start >= groupEnd)
            {
                Flush();
            }

            var used = new HashSet<int>();
            foreach (var (item, col) in group)
            {
                if (item.End > ev.Start)
                {
                    used.Add(col);
                }
            }

            int c = 0;
            while (used.Contains(c))
            {
                c++;
            }

            group.Add((ev, c));
            groupEnd = (groupEnd is null || ev.End > groupEnd) ? ev.End : groupEnd;
        }

        Flush();
        return result;
    }
}
