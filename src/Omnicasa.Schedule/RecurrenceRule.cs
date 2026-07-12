namespace Omnicasa.Schedule;

/// <summary>How often a <see cref="RecurrenceRule"/> repeats.</summary>
public enum RecurrenceFrequency
{
    /// <summary>Repeat every N days.</summary>
    Daily,

    /// <summary>Repeat every N weeks, on one or more weekdays.</summary>
    Weekly,

    /// <summary>Repeat every N months, on the same day-of-month.</summary>
    Monthly,
}

/// <summary>
/// Describes how a template appointment repeats. Consumed by <see cref="RecurrenceExpander"/> to
/// produce concrete occurrences; the library never applies a rule to a live item on its own.
/// </summary>
public sealed class RecurrenceRule
{
    /// <summary>Gets or sets how often the rule repeats.</summary>
    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Gets or sets the step between occurrences (in <see cref="Frequency"/> units, minimum 1).</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Gets or sets the maximum number of occurrences to emit, or null for unbounded.</summary>
    public int? Count { get; set; }

    /// <summary>Gets or sets the inclusive last date an occurrence may start on, or null for unbounded.</summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// Gets or sets the weekdays a <see cref="RecurrenceFrequency.Weekly"/> rule fires on.
    /// When null, the template's own weekday is used.
    /// </summary>
    public IReadOnlyList<DayOfWeek>? ByWeekday { get; set; }
}
