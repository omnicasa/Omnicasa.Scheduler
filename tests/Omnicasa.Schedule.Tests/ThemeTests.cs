using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ThemeTests
{
    [Fact]
    public void ScheduleViewTheme_HasExpectedColorDefaults()
    {
        var theme = new ScheduleViewTheme();

        Assert.Equal(Colors.White, theme.Background);
        Assert.Equal(Colors.Black, theme.Foreground);
        Assert.Equal(Color.FromArgb("#8E8E93"), theme.Muted);
        Assert.Equal(Color.FromArgb("#FF3B30"), theme.Accent);
        Assert.Equal(Color.FromArgb("#E5E5EA"), theme.GridLine);
        Assert.Equal(Color.FromArgb("#FF3B30"), theme.Today);
    }

    [Fact]
    public void ScheduleViewTheme_HasExpectedNumericDefaults()
    {
        var theme = new ScheduleViewTheme();

        Assert.Equal(11.0, theme.HourLabelFontSize);
        Assert.Equal(12.0, theme.HeaderPrimaryFontSize);
        Assert.Equal(18.0, theme.HeaderSecondaryFontSize);
        Assert.Equal(12.0, theme.BlockTitleFontSize);
        Assert.Equal(10.0, theme.BlockRangeFontSize);
        Assert.Equal(56.0, theme.TimeRailWidth);
        Assert.Equal(48.0, theme.HeaderHeight);
    }

    [Fact]
    public void ScheduleViewTheme_PropertiesAreSettable()
    {
        var theme = new ScheduleViewTheme { Accent = Colors.DodgerBlue };
        theme.HeaderHeight = 64;

        Assert.Equal(Colors.DodgerBlue, theme.Accent);
        Assert.Equal(64.0, theme.HeaderHeight);
    }

    [Fact]
    public void ScheduleTheme_HasExpectedColorDefaults()
    {
        var theme = new ScheduleTheme();

        Assert.Equal(Colors.White, theme.Background);
        Assert.Equal(Colors.Black, theme.Foreground);
        Assert.Equal(Color.FromArgb("#8E8E93"), theme.Muted);
        Assert.Equal(Color.FromArgb("#FF3B30"), theme.Accent);
        Assert.Equal(Color.FromArgb("#E5E5EA"), theme.GridLine);
        Assert.Equal(Color.FromArgb("#FF3B30"), theme.Today);
        Assert.Equal(Color.FromArgb("#8E8E93"), theme.Saturday);
        Assert.Equal(Color.FromArgb("#8E8E93"), theme.Sunday);
    }
}
