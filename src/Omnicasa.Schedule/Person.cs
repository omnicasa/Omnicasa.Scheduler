using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>Default <see cref="IPerson"/> implementation; a bindable person / resource whose appointments occupy a dedicated column.</summary>
public class Person : BindableObject, IPerson
{
    /// <summary>Bindable property for <see cref="Id"/>.</summary>
    public static readonly BindableProperty IdProperty =
        BindableProperty.Create(nameof(Id), typeof(string), typeof(Person), string.Empty);

    /// <summary>Bindable property for <see cref="Name"/>.</summary>
    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(nameof(Name), typeof(string), typeof(Person), string.Empty);

    /// <summary>Bindable property for <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(Person), null);

    /// <summary>Gets or sets the stable identifier used to link appointments to this person.</summary>
    public string Id
    {
        get => (string)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>Gets or sets the display name shown in the column header.</summary>
    public string Name
    {
        get => (string)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>Gets or sets the accent color used for the column header and default block color.</summary>
    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }
}
