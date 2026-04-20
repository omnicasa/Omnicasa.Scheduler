using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>Color palette applied by the schedule controls when drawing.</summary>
public sealed class ScheduleTheme : BindableObject
{
    /// <summary>Bindable property for <see cref="Background"/>.</summary>
    public static readonly BindableProperty BackgroundProperty =
        BindableProperty.Create(nameof(Background), typeof(Color), typeof(ScheduleTheme), Colors.White);

    /// <summary>Bindable property for <see cref="Foreground"/>.</summary>
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(Color), typeof(ScheduleTheme), Colors.Black);

    /// <summary>Bindable property for <see cref="Muted"/>.</summary>
    public static readonly BindableProperty MutedProperty =
        BindableProperty.Create(nameof(Muted), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#8E8E93"));

    /// <summary>Bindable property for <see cref="Accent"/>.</summary>
    public static readonly BindableProperty AccentProperty =
        BindableProperty.Create(nameof(Accent), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#FF3B30"));

    /// <summary>Bindable property for <see cref="GridLine"/>.</summary>
    public static readonly BindableProperty GridLineProperty =
        BindableProperty.Create(nameof(GridLine), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#E5E5EA"));

    /// <summary>Bindable property for <see cref="Today"/>.</summary>
    public static readonly BindableProperty TodayProperty =
        BindableProperty.Create(nameof(Today), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#FF3B30"));

    /// <summary>Bindable property for <see cref="Saturday"/>.</summary>
    public static readonly BindableProperty SaturdayProperty =
        BindableProperty.Create(nameof(Saturday), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#8E8E93"));

    /// <summary>Bindable property for <see cref="Sunday"/>.</summary>
    public static readonly BindableProperty SundayProperty =
        BindableProperty.Create(nameof(Sunday), typeof(Color), typeof(ScheduleTheme), Color.FromArgb("#8E8E93"));

    /// <summary>Gets or sets the background color of the calendar surface.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the primary foreground (text) color.</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the color used for low-emphasis text.</summary>
    public Color Muted
    {
        get => (Color)GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }

    /// <summary>Gets or sets the accent (highlight) color.</summary>
    public Color Accent
    {
        get => (Color)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Gets or sets the color used for hour and month grid lines.</summary>
    public Color GridLine
    {
        get => (Color)GetValue(GridLineProperty);
        set => SetValue(GridLineProperty, value);
    }

    /// <summary>Gets or sets the color indicating the current day or current-time marker.</summary>
    public Color Today
    {
        get => (Color)GetValue(TodayProperty);
        set => SetValue(TodayProperty, value);
    }

    /// <summary>Gets or sets the color applied to Saturday day labels.</summary>
    public Color Saturday
    {
        get => (Color)GetValue(SaturdayProperty);
        set => SetValue(SaturdayProperty, value);
    }

    /// <summary>Gets or sets the color applied to Sunday day labels.</summary>
    public Color Sunday
    {
        get => (Color)GetValue(SundayProperty);
        set => SetValue(SundayProperty, value);
    }
}
