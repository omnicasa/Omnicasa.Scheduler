using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class BlockoutTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    [Fact]
    public void DrawBlockout_Default_FillsRectAtLowAlpha()
    {
        var canvas = new RecordingCanvas();
        var rect = new RectF(10, 100, 120, 60);

        new ScheduleViewRenderer().DrawBlockout(new ScheduleBlockoutContext
        {
            Canvas = canvas,
            Blockout = new TestBlockout { Start = Day.AddHours(9), End = Day.AddHours(10) },
            Rect = rect,
            Color = Colors.Gray,
            Theme = new ScheduleViewTheme(),
        });

        Assert.Contains(rect, canvas.FilledRectangles);
        Assert.NotNull(canvas.LastFillColor);
        Assert.True(canvas.LastFillColor!.Alpha < 0.5f);   // translucent band
    }

    [Fact]
    public void DrawBlockout_WithTitle_DrawsCaption()
    {
        var canvas = new RecordingCanvas();

        new ScheduleViewRenderer().DrawBlockout(new ScheduleBlockoutContext
        {
            Canvas = canvas,
            Blockout = new TestBlockout { Start = Day.AddHours(12), End = Day.AddHours(13), Title = "Lunch" },
            Rect = new RectF(0, 0, 120, 60),
            Color = Colors.Orange,
            Theme = new ScheduleViewTheme(),
        });

        Assert.Contains("Lunch", canvas.Strings);
    }

    [Fact]
    public void BodyDrawable_BlockoutMapsToExpectedYRangeViaTimeScale()
    {
        var canvas = new RecordingCanvas();
        var renderer = new BlockoutMarkerRenderer();
        var scale = new TimeScale(60);
        var drawable = new ScheduleBodyDrawable
        {
            Renderer = renderer,
            Context = new ScheduleRenderContext
            {
                Theme = new ScheduleViewTheme(),
                Scale = scale,
                TimeRailWidth = 56,
                Columns = new[] { new ScheduleViewColumn { DayStart = Day } },
                Blockouts = new List<IScheduleBlockout>
                {
                    new TestBlockout { Start = Day.AddHours(10), End = Day.AddHours(12) },
                },
            },
        };

        drawable.Draw(canvas, new RectF(0, 0, 320, scale.TotalHeight));

        Assert.NotNull(renderer.Rect);
        Assert.Equal(scale.YForTime(TimeSpan.FromHours(10)), renderer.Rect!.Value.Top, 3);
        Assert.Equal(scale.YForTime(TimeSpan.FromHours(12)), renderer.Rect.Value.Bottom, 3);
    }

    [Fact]
    public void BodyDrawable_PersonScopedBlockout_OnlyPaintsMatchingColumn()
    {
        var canvas = new RecordingCanvas();
        var renderer = new BlockoutMarkerRenderer();
        var drawable = new ScheduleBodyDrawable
        {
            Renderer = renderer,
            Context = new ScheduleRenderContext
            {
                Theme = new ScheduleViewTheme(),
                Scale = new TimeScale(60),
                TimeRailWidth = 56,
                Columns = new[]
                {
                    new ScheduleViewColumn { DayStart = Day, PersonId = "p1" },
                    new ScheduleViewColumn { DayStart = Day, PersonId = "p2" },
                },
                Blockouts = new List<IScheduleBlockout>
                {
                    new TestBlockout { Start = Day.AddHours(9), End = Day.AddHours(10), PersonId = "p2" },
                },
            },
        };

        drawable.Draw(canvas, new RectF(0, 0, 320, new TimeScale(60).TotalHeight));

        // Only the p2 column band is painted.
        Assert.Equal(1, renderer.Count);
    }

    private sealed class TestBlockout : IScheduleBlockout
    {
        public DateTime Start { get; init; }

        public DateTime End { get; init; }

        public string? PersonId { get; init; }

        public string? Title { get; init; }

        public Color? Color { get; init; }
    }

    private sealed class BlockoutMarkerRenderer : ScheduleViewRenderer
    {
        public RectF? Rect { get; private set; }

        public int Count { get; private set; }

        public override void DrawBlockout(ScheduleBlockoutContext ctx)
        {
            Rect = ctx.Rect;
            Count++;
        }
    }
}
