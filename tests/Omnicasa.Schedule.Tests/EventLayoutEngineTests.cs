using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class EventLayoutEngineTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static Appointment Appt(double startHour, double endHour, string id, bool allDay = false) =>
        new Appointment
        {
            Id = id,
            Start = Day.AddHours(startHour),
            End = Day.AddHours(endHour),
            IsAllDay = allDay,
        };

    [Fact]
    public void Layout_ExcludesAllDay()
    {
        var result = EventLayoutEngine.Layout(new[] { Appt(9, 10, "a"), Appt(0, 24, "allday", allDay: true) });

        Assert.Single(result);
        Assert.Equal("a", result[0].Appointment.Id);
    }

    [Fact]
    public void Layout_OverlappingPair_SplitsIntoTwoColumns()
    {
        var result = EventLayoutEngine.Layout(new[] { Appt(9, 10.5, "a"), Appt(9.5, 11, "b") });

        Assert.All(result, r => Assert.Equal(2, r.ColumnsInGroup));
        Assert.Equal(new[] { 0, 1 }, result.OrderBy(r => r.Column).Select(r => r.Column));
    }

    [Fact]
    public void Layout_SeparateItems_AreSingleColumnGroups()
    {
        var result = EventLayoutEngine.Layout(new[] { Appt(9, 10, "a"), Appt(13, 14, "b") });

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.ColumnsInGroup));
        Assert.All(result, r => Assert.Equal(0, r.Column));
    }
}
