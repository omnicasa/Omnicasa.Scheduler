using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ScheduleMenuActionTests
{
    [Fact]
    public void Defaults_NoIconNotDestructive()
    {
        var action = new ScheduleMenuAction("Edit");

        Assert.Equal("Edit", action.Label);
        Assert.Null(action.Icon);
        Assert.False(action.IsDestructive);
    }

    [Fact]
    public void StoresLabelIconAndDestructiveFlag()
    {
        var action = new ScheduleMenuAction("Delete", icon: "trash", isDestructive: true);

        Assert.Equal("Delete", action.Label);
        Assert.Equal("trash", action.Icon);
        Assert.True(action.IsDestructive);
    }

    [Fact]
    public void Icon_IsOptional_WhileDestructiveSet()
    {
        var action = new ScheduleMenuAction("Archive", isDestructive: true);

        Assert.Null(action.Icon);
        Assert.True(action.IsDestructive);
    }
}
