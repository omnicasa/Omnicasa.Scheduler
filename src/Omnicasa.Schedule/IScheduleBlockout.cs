using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// An "unavailable" time range (out-of-office, holiday, closed hours) painted as a translucent
/// background band behind the appointments in <see cref="ScheduleView"/>. A null
/// <see cref="PersonId"/> applies the band to every column of the day; otherwise it is scoped to
/// the matching person's column.
/// </summary>
public interface IScheduleBlockout
{
    /// <summary>Start date and time of the unavailable range.</summary>
    DateTime Start { get; }

    /// <summary>End date and time of the unavailable range (must be greater than <see cref="Start"/>).</summary>
    DateTime End { get; }

    /// <summary>Optional <see cref="IPerson.Id"/> link; null bands span every column of the day.</summary>
    string? PersonId { get; }

    /// <summary>Optional caption drawn inside the band (e.g. "Lunch", "Closed").</summary>
    string? Title { get; }

    /// <summary>Optional band color; falls back to the theme's muted color.</summary>
    Color? Color { get; }
}

/// <summary>Default <see cref="IScheduleBlockout"/> implementation; a bindable unavailable time band.</summary>
public class ScheduleBlockout : BindableObject, IScheduleBlockout
{
    /// <summary>Bindable property for <see cref="Start"/>.</summary>
    public static readonly BindableProperty StartProperty =
        BindableProperty.Create(nameof(Start), typeof(DateTime), typeof(ScheduleBlockout), DateTime.MinValue);

    /// <summary>Bindable property for <see cref="End"/>.</summary>
    public static readonly BindableProperty EndProperty =
        BindableProperty.Create(nameof(End), typeof(DateTime), typeof(ScheduleBlockout), DateTime.MinValue);

    /// <summary>Bindable property for <see cref="PersonId"/>.</summary>
    public static readonly BindableProperty PersonIdProperty =
        BindableProperty.Create(nameof(PersonId), typeof(string), typeof(ScheduleBlockout), null);

    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ScheduleBlockout), null);

    /// <summary>Bindable property for <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(ScheduleBlockout), null);

    /// <summary>Gets or sets the start date and time of the unavailable range.</summary>
    public DateTime Start
    {
        get => (DateTime)GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    /// <summary>Gets or sets the end date and time of the unavailable range.</summary>
    public DateTime End
    {
        get => (DateTime)GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    /// <summary>Gets or sets the optional person this band is scoped to; null spans the whole day.</summary>
    public string? PersonId
    {
        get => (string?)GetValue(PersonIdProperty);
        set => SetValue(PersonIdProperty, value);
    }

    /// <summary>Gets or sets the optional caption drawn inside the band.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the optional band color; falls back to the theme's muted color.</summary>
    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }
}
