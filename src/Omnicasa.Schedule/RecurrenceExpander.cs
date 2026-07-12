using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Expands a recurring template <see cref="IScheduleItem"/> into concrete occurrences over a window.
/// Pure logic — it never mutates the template and produces lightweight standalone items you can feed
/// straight into <see cref="ScheduleView.ItemsSource"/>.
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// Expands <paramref name="template"/> according to <paramref name="rule"/>, returning every
    /// occurrence that intersects <paramref name="windowStart"/>..<paramref name="windowEnd"/>.
    /// Each occurrence keeps the template's time-of-day and duration and gets a unique id.
    /// </summary>
    /// <param name="template">The template item supplying time-of-day, duration and metadata.</param>
    /// <param name="rule">The recurrence rule describing how the template repeats.</param>
    /// <param name="windowStart">Inclusive start of the window to materialize.</param>
    /// <param name="windowEnd">Exclusive end of the window to materialize.</param>
    /// <returns>Occurrences intersecting the window, in chronological order.</returns>
    public static IReadOnlyList<IScheduleItem> Expand(
        IScheduleItem template,
        RecurrenceRule rule,
        DateTime windowStart,
        DateTime windowEnd)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(rule);

        var results = new List<IScheduleItem>();
        var duration = template.End - template.Start;
        int index = 0;

        foreach (var occStart in EnumerateStarts(template, rule, windowEnd))
        {
            // Stop at the first of: Until passed, or past the window end.
            if (rule.Until.HasValue && occStart.Date > rule.Until.Value.Date)
            {
                break;
            }

            if (occStart > windowEnd)
            {
                break;
            }

            var occEnd = occStart + duration;

            // Only keep occurrences that actually overlap the window (occStart is already <= windowEnd).
            if (occEnd > windowStart)
            {
                results.Add(new ExpandedScheduleItem
                {
                    Id = template.Id + "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Title = template.Title,
                    Start = occStart,
                    End = occEnd,
                    IsAllDay = template.IsAllDay,
                    Color = template.Color,
                    PersonId = template.PersonId,
                    Notes = template.Notes,
                });
            }

            index++;

            // Stop once the requested occurrence count has been generated.
            if (rule.Count.HasValue && index >= rule.Count.Value)
            {
                break;
            }
        }

        return results;
    }

    // Yields occurrence start times in chronological order up to windowEnd; the caller decides when to stop.
    private static IEnumerable<DateTime> EnumerateStarts(IScheduleItem template, RecurrenceRule rule, DateTime windowEnd)
    {
        int interval = Math.Max(1, rule.Interval);
        var timeOfDay = template.Start.TimeOfDay;
        var startDate = template.Start.Date;

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Weekly:
                var weekdays = OrderedWeekdays(rule, startDate.DayOfWeek);
                var week0 = startDate.AddDays(-(int)startDate.DayOfWeek);
                for (int k = 0; week0.AddDays(7 * interval * k) <= windowEnd; k++)
                {
                    var weekStart = week0.AddDays(7 * interval * k);
                    foreach (var dow in weekdays)
                    {
                        var date = weekStart.AddDays((int)dow);
                        if (date < startDate)
                        {
                            continue; // skip weekdays before the template start in the first week
                        }

                        yield return date + timeOfDay;
                    }
                }

                break;

            case RecurrenceFrequency.Monthly:
                int day = startDate.Day;
                var firstOfMonth = new DateTime(startDate.Year, startDate.Month, 1);
                for (int k = 0; firstOfMonth.AddMonths(interval * k) <= windowEnd; k++)
                {
                    var month = firstOfMonth.AddMonths(interval * k);
                    if (DateTime.DaysInMonth(month.Year, month.Month) >= day)
                    {
                        yield return new DateTime(month.Year, month.Month, day) + timeOfDay;
                    }
                }

                break;

            default: // Daily
                for (var cursor = startDate; cursor <= windowEnd; cursor = cursor.AddDays(interval))
                {
                    yield return cursor + timeOfDay;
                }

                break;
        }
    }

    private static IReadOnlyList<DayOfWeek> OrderedWeekdays(RecurrenceRule rule, DayOfWeek templateDay)
    {
        if (rule.ByWeekday is { Count: > 0 })
        {
            var set = new SortedSet<DayOfWeek>(rule.ByWeekday);
            return new List<DayOfWeek>(set);
        }

        return new[] { templateDay };
    }

    /// <summary>A standalone occurrence produced by <see cref="RecurrenceExpander"/>.</summary>
    private sealed class ExpandedScheduleItem : IScheduleItem
    {
        public string Id { get; init; } = string.Empty;

        public string? Title { get; init; }

        public DateTime Start { get; init; }

        public DateTime End { get; init; }

        public bool IsAllDay { get; init; }

        public Color? Color { get; init; }

        public string? PersonId { get; init; }

        public string? Notes { get; init; }
    }
}
