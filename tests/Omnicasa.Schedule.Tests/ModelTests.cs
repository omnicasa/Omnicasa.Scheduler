using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using Xunit;

namespace Omnicasa.Schedule.Tests;

public class ModelTests
{
    [Fact]
    public void Appointment_Duration_IsEndMinusStart()
    {
        var a = new Appointment
        {
            Start = new DateTime(2026, 5, 25, 9, 0, 0),
            End = new DateTime(2026, 5, 25, 10, 30, 0),
        };

        Assert.Equal(TimeSpan.FromMinutes(90), a.Duration);
    }

    [Fact]
    public void Appointment_ImplementsIScheduleItem()
    {
        IScheduleItem item = new Appointment { Id = "x", Title = "Demo", PersonId = "p1" };

        Assert.Equal("x", item.Id);
        Assert.Equal("Demo", item.Title);
        Assert.Equal("p1", item.PersonId);
    }

    [Fact]
    public void Appointment_StoresUserDataPayload()
    {
        var payload = new { Kind = "meeting" };
        var a = new Appointment { UserData = payload };

        Assert.Same(payload, a.UserData);
    }

    [Fact]
    public void Person_ImplementsIPerson()
    {
        IPerson person = new Person { Id = "p1", Name = "Alice", Color = Colors.DodgerBlue };

        Assert.Equal("p1", person.Id);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(Colors.DodgerBlue, person.Color);
    }

    [Fact]
    public void Person_DefaultsAreEmptyStrings()
    {
        var person = new Person();

        Assert.Equal(string.Empty, person.Id);
        Assert.Equal(string.Empty, person.Name);
        Assert.Null(person.Color);
    }
}
