using System.ComponentModel;
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

/// <summary>A minimal <see cref="ITypingScheduleItem"/> for renderer tests.</summary>
internal sealed class TestTypingItem : ITypingScheduleItem
{
    private DateTime start;
    private DateTime end;
    private string? personId;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string? Title { get; init; }

    public DateTime Start
    {
        get => start;
        set => Set(ref start, value);
    }

    public DateTime End
    {
        get => end;
        set => Set(ref end, value);
    }

    public bool IsAllDay { get; init; }

    public Color? Color { get; init; }

    public string? PersonId
    {
        get => personId;
        set => Set(ref personId, value);
    }

    public string? Notes { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
