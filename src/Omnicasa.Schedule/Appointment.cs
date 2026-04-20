using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Represents a single calendar appointment or event rendered by the schedule controls.
/// </summary>
public class Appointment : BindableObject
{
    /// <summary>Bindable property for <see cref="Id"/>.</summary>
    public static readonly BindableProperty IdProperty =
        BindableProperty.Create(nameof(Id), typeof(string), typeof(Appointment), string.Empty);

    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(Appointment), string.Empty);

    /// <summary>Bindable property for <see cref="Start"/>.</summary>
    public static readonly BindableProperty StartProperty =
        BindableProperty.Create(nameof(Start), typeof(DateTime), typeof(Appointment), DateTime.MinValue);

    /// <summary>Bindable property for <see cref="End"/>.</summary>
    public static readonly BindableProperty EndProperty =
        BindableProperty.Create(nameof(End), typeof(DateTime), typeof(Appointment), DateTime.MinValue);

    /// <summary>Bindable property for <see cref="IsAllDay"/>.</summary>
    public static readonly BindableProperty IsAllDayProperty =
        BindableProperty.Create(nameof(IsAllDay), typeof(bool), typeof(Appointment), false);

    /// <summary>Bindable property for <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(Appointment), null);

    /// <summary>Bindable property for <see cref="Location"/>.</summary>
    public static readonly BindableProperty LocationProperty =
        BindableProperty.Create(nameof(Location), typeof(string), typeof(Appointment), null);

    /// <summary>Bindable property for <see cref="Notes"/>.</summary>
    public static readonly BindableProperty NotesProperty =
        BindableProperty.Create(nameof(Notes), typeof(string), typeof(Appointment), null);

    /// <summary>Gets or sets the stable identifier of the appointment.</summary>
    public string Id
    {
        get => (string)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>Gets or sets the title shown on the appointment block.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the start date and time of the appointment.</summary>
    public DateTime Start
    {
        get => (DateTime)GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    /// <summary>Gets or sets the end date and time of the appointment.</summary>
    public DateTime End
    {
        get => (DateTime)GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the appointment spans the whole day.</summary>
    public bool IsAllDay
    {
        get => (bool)GetValue(IsAllDayProperty);
        set => SetValue(IsAllDayProperty, value);
    }

    /// <summary>Gets or sets the color used to render the appointment block.</summary>
    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Gets or sets the display location associated with the appointment.</summary>
    public string? Location
    {
        get => (string?)GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    /// <summary>Gets or sets free-form notes for the appointment.</summary>
    public string? Notes
    {
        get => (string?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>Gets or sets an arbitrary payload carried with the appointment.</summary>
    public object? UserData { get; set; }

    /// <summary>Gets the duration of the appointment.</summary>
    public TimeSpan Duration => End - Start;
}
