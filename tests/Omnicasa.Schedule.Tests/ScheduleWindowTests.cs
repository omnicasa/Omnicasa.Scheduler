using Omnicasa.Schedule.Widget;
using Xunit;

namespace Omnicasa.Schedule.Tests;

/// <summary>
/// Exercises the widget's shared window/column-layout logic (the pure part of the native widget
/// reference sources under <c>widgets/android</c>). The Canvas→Bitmap renderer needs Android and is
/// validated on-device instead.
/// </summary>
public class ScheduleWindowTests
{
    private static readonly DateTime Day = new DateTime(2026, 5, 25);

    private static WidgetAppointment Appt(int startHour, int startMin, int endHour, int endMin, string id = "x", string? color = null) =>
        new WidgetAppointment
        {
            AppointmentId = id.GetHashCode(),
            Title = id,
            StartTime = Day.AddHours(startHour).AddMinutes(startMin).ToString(WidgetAppointment.DateFormat),
            EndTime = Day.AddHours(endHour).AddMinutes(endMin).ToString(WidgetAppointment.DateFormat),
            BackgroundColor = color,
        };

    [Fact]
    public void WidgetAppointment_ParsesSharedDateFormat()
    {
        var a = Appt(9, 0, 10, 30, "meeting");

        Assert.Equal(new DateTime(2026, 5, 25, 9, 0, 0), a.Start);
        Assert.Equal(new DateTime(2026, 5, 25, 10, 30, 0), a.End);
    }

    [Fact]
    public void WidgetAppointmentShare_Read_ParsesContractJson()
    {
        const string json = """
        { "Time": "2026-05-25 08:00:00", "Appointments": [
            { "AppointmentId": 1, "Title": "Standup", "StartTime": "2026-05-25 09:00:00", "EndTime": "2026-05-25 09:30:00", "BackgroundColor": "#007AFF" }
        ] }
        """;

        var items = WidgetAppointmentShare.Read(json);

        var item = Assert.Single(items);
        Assert.Equal("Standup", item.Title);
        Assert.Equal(new DateTime(2026, 5, 25, 9, 0, 0), item.Start);
    }

    [Fact]
    public void WidgetAppointmentShare_Read_MalformedJson_ReturnsEmpty()
    {
        Assert.Empty(WidgetAppointmentShare.Read("not json"));
    }

    [Fact]
    public void Build_WindowSpansRequestedHoursAndIsClampedToDay()
    {
        var now = Day.AddHours(9);
        var window = ScheduleWindow.Build(Array.Empty<WidgetAppointment>(), Day, now, windowHours: 6);

        Assert.Equal(6 * 60, window.SpanMinutes);
        Assert.True(window.StartMinutes >= 0);
        Assert.True(window.EndMinutes <= 24 * 60);
    }

    [Fact]
    public void Build_AnchorsWindowAroundNow_WithOneHourLeadIn()
    {
        var now = Day.AddHours(10);
        var window = ScheduleWindow.Build(Array.Empty<WidgetAppointment>(), Day, now, windowHours: 6);

        // One hour of lead-in, floored to the hour: 10:00 -> window starts at 09:00.
        Assert.Equal(9 * 60, window.StartMinutes);
        Assert.Equal(10 * 60, window.NowMinutes);
    }

    [Fact]
    public void Build_LateInDay_ClampsWindowSoItFits()
    {
        var now = Day.AddHours(23);
        var window = ScheduleWindow.Build(Array.Empty<WidgetAppointment>(), Day, now, windowHours: 6);

        // Window can't start past 18:00 for a 6h span ending at midnight.
        Assert.Equal(18 * 60, window.StartMinutes);
        Assert.Equal(24 * 60, window.EndMinutes);
    }

    [Fact]
    public void Build_NotToday_HasNoNowLine()
    {
        var now = Day.AddDays(1).AddHours(10);   // viewing Day, but "now" is tomorrow
        var window = ScheduleWindow.Build(new[] { Appt(9, 0, 10, 0) }, Day, now);

        Assert.Null(window.NowMinutes);
    }

    [Fact]
    public void Build_OnlyIncludesItemsIntersectingTheWindow()
    {
        var now = Day.AddHours(10);   // window 09:00–15:00
        var items = new[]
        {
            Appt(9, 30, 10, 0, "in"),
            Appt(2, 0, 3, 0, "earlyOut"),
            Appt(20, 0, 21, 0, "lateOut"),
        };

        var window = ScheduleWindow.Build(items, Day, now, windowHours: 6);

        Assert.Single(window.Items);
        Assert.Equal("in", window.Items[0].Appointment.Title);
    }

    [Fact]
    public void Build_OverlappingItems_GetSeparateColumns()
    {
        var now = Day.AddHours(9);
        var items = new[]
        {
            Appt(9, 30, 10, 30, "a"),
            Appt(10, 0, 11, 0, "b"),   // overlaps a
        };

        var window = ScheduleWindow.Build(items, Day, now, windowHours: 6);

        Assert.Equal(2, window.Items.Count);
        Assert.All(window.Items, i => Assert.Equal(2, i.ColumnsInGroup));
        Assert.Equal(new[] { 0, 1 }, window.Items.OrderBy(i => i.Column).Select(i => i.Column));
    }

    [Fact]
    public void Build_AllDayLikeItemsOutsideWindow_AreExcluded()
    {
        // A midnight-to-midnight item won't intersect a mid-day window's content meaningfully here;
        // the grid is time-based, so only items overlapping the visible hours are laid out.
        var now = Day.AddHours(10);
        var items = new[] { Appt(0, 0, 0, 0, "zeroLength") };

        var window = ScheduleWindow.Build(items, Day, now, windowHours: 6);

        Assert.Empty(window.Items);   // zero-length (end <= start) is dropped
    }
}
