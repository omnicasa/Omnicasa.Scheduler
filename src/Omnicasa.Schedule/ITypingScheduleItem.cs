using System.ComponentModel;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// A mutable "draft" appointment used with <c>ScheduleView.TypingItem</c>. Looks like
/// <see cref="IScheduleItem"/> but exposes setters for the fields a user can change by direct
/// manipulation: <see cref="Start"/>, <see cref="End"/>, and <see cref="PersonId"/>.
/// Implementers should raise <see cref="INotifyPropertyChanged.PropertyChanged"/> so the view
/// re-renders when the model is updated from outside as well.
/// </summary>
public interface ITypingScheduleItem : INotifyPropertyChanged
{
    /// <summary>Stable identifier.</summary>
    string Id { get; }

    /// <summary>Title rendered on the draft block.</summary>
    string? Title { get; }

    /// <summary>Block start; mutated when the user moves the block or drags the top edge.</summary>
    DateTime Start { get; set; }

    /// <summary>Block end; mutated when the user moves the block or drags the bottom edge.</summary>
    DateTime End { get; set; }

    /// <summary>Whether the draft is an all-day item (currently not rendered in the time grid).</summary>
    bool IsAllDay { get; }

    /// <summary>Optional block color. Falls back to the column accent or theme accent.</summary>
    Color? Color { get; }

    /// <summary>Person assignment; mutated when the block is dragged into a different person's column.</summary>
    string? PersonId { get; set; }

    /// <summary>Free-form notes.</summary>
    string? Notes { get; }
}
