using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Minimal contract for a person / resource whose appointments occupy a dedicated column.
/// Implement this on your own model classes, or use the provided <see cref="Person"/>.
/// Items are linked to a person via <see cref="IScheduleItem.PersonId"/> matching <see cref="Id"/>.
/// </summary>
public interface IPerson
{
    /// <summary>Stable identifier used to link appointments to this person.</summary>
    string Id { get; }

    /// <summary>Display name shown in the column header.</summary>
    string? Name { get; }

    /// <summary>Accent color used for the column header and default block color.</summary>
    Color? Color { get; }
}
