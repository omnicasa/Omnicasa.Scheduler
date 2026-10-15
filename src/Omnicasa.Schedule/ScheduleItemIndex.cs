namespace Omnicasa.Schedule;

/// <summary>Buckets schedule items under every day they overlap.</summary>
public static class ScheduleItemIndex
{
    /// <summary>
    /// Builds a new index. The result is never mutated afterwards, so a reader on another thread can
    /// keep using an old index while a new one is swapped in.
    /// </summary>
    /// <param name="items">Items to index.</param>
    /// <returns>Items keyed by each day they touch.</returns>
    public static Dictionary<DateOnly, List<IScheduleItem>> Build(IEnumerable<IScheduleItem> items)
    {
        var index = new Dictionary<DateOnly, List<IScheduleItem>>();
        foreach (var item in items)
        {
            for (var d = item.Start.Date; d <= item.End.Date; d = d.AddDays(1))
            {
                var key = DateOnly.FromDateTime(d);
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<IScheduleItem>();
                    index[key] = list;
                }

                list.Add(item);
            }
        }

        return index;
    }
}
