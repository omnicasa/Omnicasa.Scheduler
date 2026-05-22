namespace Omnicasa.Schedule;

/// <summary>An <see cref="IScheduleItem"/> positioned within an overlap group for column rendering.</summary>
public sealed class LaidOutItem
{
    /// <summary>Initializes a new instance of the <see cref="LaidOutItem"/> class.</summary>
    /// <param name="item">The underlying item.</param>
    /// <param name="column">Zero-based column the item occupies within its overlap group.</param>
    /// <param name="columnsInGroup">Total number of columns in the overlap group.</param>
    public LaidOutItem(IScheduleItem item, int column, int columnsInGroup)
    {
        Item = item;
        Column = column;
        ColumnsInGroup = columnsInGroup;
    }

    /// <summary>Gets the underlying item.</summary>
    public IScheduleItem Item { get; }

    /// <summary>Gets the zero-based column the item occupies within its overlap group.</summary>
    public int Column { get; }

    /// <summary>Gets the total number of columns in the overlap group.</summary>
    public int ColumnsInGroup { get; }
}

/// <summary>
/// Assigns each non-all-day <see cref="IScheduleItem"/> to a horizontal column within its overlap group.
/// Stateless and pure — call once per day's items.
/// </summary>
public static class ScheduleLayout
{
    /// <summary>Computes column placements for a single day's items.</summary>
    /// <param name="items">Items occurring on the same day.</param>
    /// <returns>List of laid-out items with column assignments. All-day items are excluded.</returns>
    public static IReadOnlyList<LaidOutItem> Layout(IEnumerable<IScheduleItem> items)
    {
        var sorted = items
            .Where(a => !a.IsAllDay)
            .OrderBy(a => a.Start)
            .ThenBy(a => a.End)
            .ToList();

        var result = new List<LaidOutItem>(sorted.Count);
        var group = new List<(IScheduleItem Item, int Column)>();
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
                result.Add(new LaidOutItem(item, col, cols));
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
