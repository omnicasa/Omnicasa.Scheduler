using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleDaySummaryTests
{
    [Theory]
    [InlineData(0, "Nothing planned")]
    [InlineData(-1, "Nothing planned")]
    [InlineData(1, "1 appointment")]
    [InlineData(3, "3 appointments")]
    public void DefaultText_PluralizesTheCount(int count, string expected)
    {
        var summary = new ScheduleDaySummary { Day = new DateTime(2026, 8, 4), AppointmentCount = count };

        Assert.Equal(expected, ScheduleDaySummary.DefaultText(summary));
    }

    [Fact]
    public void DefaultText_IgnoresAllDayItems()
    {
        // All-day items are drawn as bars, so they never contribute to the summary line.
        var summary = new ScheduleDaySummary { AppointmentCount = 0, AllDayCount = 2 };

        Assert.Equal("Nothing planned", ScheduleDaySummary.DefaultText(summary));
    }
}
