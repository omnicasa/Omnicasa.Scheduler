using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

// MOBILE-12756 paint rebuilds hit maps / the day index on Android's GL thread while touch reads them.
public class PaintThreadSnapshotTests
{
    private static readonly TimeSpan RaceDuration = TimeSpan.FromMilliseconds(750);

    [Fact]
    public void SnapshotList_ReaderNeverSeesARebuildInProgress()
    {
        var map = new SnapshotList<(int Day, float X)>();

        RunRace(
            write: round =>
            {
                map.Reset();
                for (int i = 0; i < 64; i++)
                {
                    map.Pending.Add((i, round));
                }

                map.Publish();
            },
            read: () =>
            {
                var snapshot = map.Snapshot;
                int count = 0;
                foreach (var (_, x) in snapshot)
                {
                    // A published snapshot is whole: one round, all 64 cells.
                    Assert.Equal(snapshot[0].X, x);
                    count++;
                }

                Assert.True(count is 0 or 64, $"saw a partial map of {count}");
            });
    }

    [Fact]
    public void SnapshotList_PublishedListIsNotTouchedByTheNextBuild()
    {
        var map = new SnapshotList<int>();
        map.Pending.Add(1);
        map.Publish();
        var first = map.Snapshot;

        map.Reset();
        map.Pending.Add(2);
        map.Pending.Add(3);

        Assert.Equal(new[] { 1 }, first);
        Assert.Equal(new[] { 1 }, map.Snapshot);

        map.Publish();
        Assert.Equal(new[] { 2, 3 }, map.Snapshot);
        Assert.Equal(new[] { 1 }, first);
    }

    [Fact]
    public void ScheduleItemIndex_BucketsMultiDayItemsUnderEachDay()
    {
        var day = new DateTime(2026, 10, 14);
        var single = new TestScheduleItem { Start = day.AddHours(9), End = day.AddHours(10) };
        var spanning = new TestScheduleItem { Start = day.AddHours(20), End = day.AddDays(2).AddHours(1) };

        var index = ScheduleItemIndex.Build(new IScheduleItem[] { single, spanning });

        Assert.Equal(new IScheduleItem[] { single, spanning }, index[DateOnly.FromDateTime(day)]);
        Assert.Equal(new IScheduleItem[] { spanning }, index[DateOnly.FromDateTime(day.AddDays(1))]);
        Assert.Equal(new IScheduleItem[] { spanning }, index[DateOnly.FromDateTime(day.AddDays(2))]);
        Assert.Equal(3, index.Count);
    }

    [Fact]
    public void ScheduleItemIndex_SwapIsSafeWhilePaintEnumerates()
    {
        var day = new DateTime(2026, 10, 14);
        var key = DateOnly.FromDateTime(day);
        var items = Enumerable.Range(0, 32)
            .Select(i => (IScheduleItem)new TestScheduleItem { Start = day.AddMinutes(i * 15), End = day.AddMinutes((i * 15) + 10) })
            .ToList();
        var index = ScheduleItemIndex.Build(items);

        RunRace(
            write: _ => Volatile.Write(ref index, ScheduleItemIndex.Build(items)),
            read: () =>
            {
                // Same shape as InfiniteScheduleView.TimedItemCount.
                if (Volatile.Read(ref index).TryGetValue(key, out var dayItems))
                {
                    int count = 0;
                    foreach (var item in dayItems)
                    {
                        count++;
                    }

                    Assert.Equal(32, count);
                }
            });
    }

    // Runs write on one thread and read on another until RaceDuration elapses; rethrows the first failure.
    private static void RunRace(Action<int> write, Action read)
    {
        Exception? failure = null;
        using var stop = new CancellationTokenSource(RaceDuration);

        var writer = new Thread(() =>
        {
            try
            {
                for (int round = 0; !stop.IsCancellationRequested; round++)
                {
                    write(round);
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref failure, ex, null);
                stop.Cancel();
            }
        });

        var reader = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    read();
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref failure, ex, null);
                stop.Cancel();
            }
        });

        writer.Start();
        reader.Start();
        writer.Join();
        reader.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"race failed: {failure}");
        }
    }
}
