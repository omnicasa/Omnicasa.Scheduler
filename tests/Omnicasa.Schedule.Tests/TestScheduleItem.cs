using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Tests;

/// <summary>A minimal <see cref="IScheduleItem"/> used to drive layout tests without MAUI bindables.</summary>
internal sealed class TestScheduleItem : IScheduleItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string? Title { get; init; }

    public DateTime Start { get; init; }

    public DateTime End { get; init; }

    public bool IsAllDay { get; init; }

    public Color? Color { get; init; }

    public string? PersonId { get; init; }

    public string? Notes { get; init; }
}
