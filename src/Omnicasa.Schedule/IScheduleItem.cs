using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Minimal contract for an appointment / event item that <see cref="ScheduleView"/> can render.
/// Implement this on your own model classes; the library never mutates the values in v1.
/// </summary>
public interface IScheduleItem
{
    /// <summary>Stable identifier (useful for deduplication and equality checks).</summary>
    string Id { get; }

    /// <summary>Title rendered on the block.</summary>
    string? Title { get; }

    /// <summary>Start date and time.</summary>
    DateTime Start { get; }

    /// <summary>End date and time (must be greater than <see cref="Start"/>).</summary>
    DateTime End { get; }

    /// <summary>True for all-day items (currently not rendered in the time grid).</summary>
    bool IsAllDay { get; }

    /// <summary>Optional block color; falls back to the person's color or the theme accent.</summary>
    Color? Color { get; }

    /// <summary>Optional <see cref="Person.Id"/> link. Required when persons are bound.</summary>
    string? PersonId { get; }

    /// <summary>Optional free-form notes.</summary>
    string? Notes { get; }
}
