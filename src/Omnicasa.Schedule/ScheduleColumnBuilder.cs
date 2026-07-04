using System.Globalization;

namespace Omnicasa.Schedule;

/// <summary>
/// Builds the day / day-person columns rendered by <see cref="ScheduleView"/> and its detached
/// header view, so a standalone header produces exactly the same column set (labels, accents,
/// today flag) as the schedule body.
/// </summary>
public static class ScheduleColumnBuilder
{
    /// <summary>
    /// Returns the effective number of day columns: <paramref name="viewMode"/> capped to the
    /// [<paramref name="startDay"/>, <paramref name="endDay"/>] range (minimum 1).
    /// </summary>
    /// <param name="startDay">First day (inclusive).</param>
    /// <param name="endDay">Last day (inclusive); clamped up to <paramref name="startDay"/>.</param>
    /// <param name="viewMode">Maximum number of day columns.</param>
    public static int EffectiveDays(DateTime startDay, DateTime endDay, int viewMode)
    {
        var rangeStart = startDay.Date;
        var rangeEnd = endDay.Date;
        if (rangeEnd < rangeStart)
        {
            rangeEnd = rangeStart;
        }

        int rangeDays = (int)(rangeEnd - rangeStart).TotalDays + 1;
        return Math.Min(Math.Max(1, viewMode), rangeDays);
    }

    /// <summary>
    /// Builds the columns for the given range, optionally split per person and populated with
    /// laid-out items. Pass no items (null) to build header-only columns.
    /// </summary>
    /// <param name="startDay">First day (inclusive).</param>
    /// <param name="endDay">Last day (inclusive).</param>
    /// <param name="viewMode">Maximum number of day columns (1..7).</param>
    /// <param name="persons">Optional persons; when non-empty each day splits into per-person sub-columns.</param>
    /// <param name="items">Optional items to lay out into the columns (spanning items are skipped).</param>
    /// <param name="today">The day highlighted as today; defaults to <see cref="DateTime.Today"/>.</param>
    /// <returns>Columns ordered left-to-right (day-major, then person).</returns>
    public static ScheduleViewColumn[] Build(
        DateTime startDay,
        DateTime endDay,
        int viewMode,
        IList<IPerson>? persons,
        IReadOnlyList<IScheduleItem>? items = null,
        DateOnly? today = null)
    {
        var personsMode = persons is not null && persons.Count > 0;
        int personCount = personsMode ? persons!.Count : 1;

        var rangeStart = startDay.Date;
        int days = EffectiveDays(startDay, endDay, viewMode);
        var todayDate = today ?? DateOnly.FromDateTime(DateTime.Today);

        var columns = new ScheduleViewColumn[days * personCount];

        for (int d = 0; d < days; d++)
        {
            var dayStart = rangeStart.AddDays(d);
            var dayEnd = dayStart.AddDays(1);
            var dayOnly = DateOnly.FromDateTime(dayStart);
            var dayShort = dayOnly.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
            var dayNum = dayOnly.Day.ToString(CultureInfo.InvariantCulture);
            var isToday = dayOnly == todayDate;

            var dayItems = items is null
                ? new List<IScheduleItem>()
                : items.Where(a => a.Start < dayEnd && a.End > dayStart && !AllDayLayout.IsSpanning(a)).ToList();

            if (personsMode)
            {
                for (int p = 0; p < personCount; p++)
                {
                    var person = persons![p];
                    var forPerson = dayItems
                        .Where(a => string.Equals(a.PersonId, person.Id, StringComparison.Ordinal))
                        .ToList();
                    columns[(d * personCount) + p] = new ScheduleViewColumn
                    {
                        DayStart = dayStart,
                        HeaderPrimary = $"{dayShort} {dayNum}",
                        HeaderSecondary = Initials(person.Name),
                        Accent = person.Color,
                        IsToday = isToday,
                        PersonId = person.Id,
                        Items = ScheduleLayout.Layout(forPerson),
                    };
                }
            }
            else
            {
                columns[d] = new ScheduleViewColumn
                {
                    DayStart = dayStart,
                    HeaderPrimary = dayShort,
                    HeaderSecondary = days > 1 ? dayNum : null,
                    Accent = null,
                    IsToday = isToday,
                    Items = ScheduleLayout.Layout(dayItems),
                };
            }
        }

        return columns;
    }

    /// <summary>Returns up-to-two uppercase initials for a person name ("?" when empty).</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(
                char.ToUpperInvariant(parts[0][0]),
                char.ToUpperInvariant(parts[^1][0]));
        }

        var single = parts[0];
        return single.Length >= 2
            ? single.Substring(0, 2).ToUpperInvariant()
            : single.ToUpperInvariant();
    }
}
