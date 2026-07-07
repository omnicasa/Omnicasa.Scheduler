using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// Bundles every visual knob used by <see cref="ScheduleView"/>: colors AND font sizes.
/// Defaults match a clean light theme; bind one instance to swap looks at runtime.
/// </summary>
public class ScheduleViewTheme : BindableObject
{
    /// <summary>Bindable property for <see cref="Background"/>.</summary>
    public static readonly BindableProperty BackgroundProperty =
        BindableProperty.Create(nameof(Background), typeof(Color), typeof(ScheduleViewTheme), Colors.White);

    /// <summary>Bindable property for <see cref="HeaderBackground"/>.</summary>
    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(nameof(HeaderBackground), typeof(Color), typeof(ScheduleViewTheme), null);

    /// <summary>Bindable property for <see cref="Foreground"/>.</summary>
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(Color), typeof(ScheduleViewTheme), Colors.Black);

    /// <summary>Bindable property for <see cref="Muted"/>.</summary>
    public static readonly BindableProperty MutedProperty =
        BindableProperty.Create(nameof(Muted), typeof(Color), typeof(ScheduleViewTheme), Color.FromArgb("#8E8E93"));

    /// <summary>Bindable property for <see cref="Accent"/>.</summary>
    public static readonly BindableProperty AccentProperty =
        BindableProperty.Create(nameof(Accent), typeof(Color), typeof(ScheduleViewTheme), Color.FromArgb("#FF3B30"));

    /// <summary>Bindable property for <see cref="GridLine"/>.</summary>
    public static readonly BindableProperty GridLineProperty =
        BindableProperty.Create(nameof(GridLine), typeof(Color), typeof(ScheduleViewTheme), Color.FromArgb("#E5E5EA"));

    /// <summary>Bindable property for <see cref="Today"/>.</summary>
    public static readonly BindableProperty TodayProperty =
        BindableProperty.Create(nameof(Today), typeof(Color), typeof(ScheduleViewTheme), Color.FromArgb("#FF3B30"));

    /// <summary>Bindable property for <see cref="NowIndicator"/>.</summary>
    public static readonly BindableProperty NowIndicatorProperty =
        BindableProperty.Create(nameof(NowIndicator), typeof(Color), typeof(ScheduleViewTheme), Color.FromArgb("#8B0000"));

    /// <summary>Bindable property for <see cref="HourLabelFontSize"/>.</summary>
    public static readonly BindableProperty HourLabelFontSizeProperty =
        BindableProperty.Create(nameof(HourLabelFontSize), typeof(double), typeof(ScheduleViewTheme), 11.0);

    /// <summary>Bindable property for <see cref="HourLabelFormat"/>.</summary>
    public static readonly BindableProperty HourLabelFormatProperty =
        BindableProperty.Create(nameof(HourLabelFormat), typeof(string), typeof(ScheduleViewTheme), null);

    /// <summary>Bindable property for <see cref="HeaderPrimaryFontSize"/>.</summary>
    public static readonly BindableProperty HeaderPrimaryFontSizeProperty =
        BindableProperty.Create(nameof(HeaderPrimaryFontSize), typeof(double), typeof(ScheduleViewTheme), 12.0);

    /// <summary>Bindable property for <see cref="HeaderSecondaryFontSize"/>.</summary>
    public static readonly BindableProperty HeaderSecondaryFontSizeProperty =
        BindableProperty.Create(nameof(HeaderSecondaryFontSize), typeof(double), typeof(ScheduleViewTheme), 18.0);

    /// <summary>Bindable property for <see cref="BlockTitleFontSize"/>.</summary>
    public static readonly BindableProperty BlockTitleFontSizeProperty =
        BindableProperty.Create(nameof(BlockTitleFontSize), typeof(double), typeof(ScheduleViewTheme), 12.0);

    /// <summary>Bindable property for <see cref="BlockRangeFontSize"/>.</summary>
    public static readonly BindableProperty BlockRangeFontSizeProperty =
        BindableProperty.Create(nameof(BlockRangeFontSize), typeof(double), typeof(ScheduleViewTheme), 10.0);

    /// <summary>Bindable property for <see cref="TimeRailWidth"/>.</summary>
    public static readonly BindableProperty TimeRailWidthProperty =
        BindableProperty.Create(nameof(TimeRailWidth), typeof(double), typeof(ScheduleViewTheme), 56.0);

    /// <summary>Bindable property for <see cref="HeaderHeight"/>.</summary>
    public static readonly BindableProperty HeaderHeightProperty =
        BindableProperty.Create(nameof(HeaderHeight), typeof(double), typeof(ScheduleViewTheme), 48.0);

    /// <summary>Background color of the canvas.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Background color of the sticky header bar; null falls back to <see cref="Background"/>.</summary>
    public Color? HeaderBackground
    {
        get => (Color?)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>Primary text color (block titles, prominent header text).</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Low-emphasis text color (hour labels, day-of-week abbreviations).</summary>
    public Color Muted
    {
        get => (Color)GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }

    /// <summary>Default block color when neither item nor person provides one.</summary>
    public Color Accent
    {
        get => (Color)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Color of the hour grid lines and column separators.</summary>
    public Color GridLine
    {
        get => (Color)GetValue(GridLineProperty);
        set => SetValue(GridLineProperty, value);
    }

    /// <summary>Color used to mark today's column header.</summary>
    public Color Today
    {
        get => (Color)GetValue(TodayProperty);
        set => SetValue(TodayProperty, value);
    }

    /// <summary>Color of the current-time marker (time capsule + full-width line). Defaults to dark red.</summary>
    public Color NowIndicator
    {
        get => (Color)GetValue(NowIndicatorProperty);
        set => SetValue(NowIndicatorProperty, value);
    }

    /// <summary>Font size of the left-rail hour labels (e.g. "9 AM").</summary>
    public double HourLabelFontSize
    {
        get => (double)GetValue(HourLabelFontSizeProperty);
        set => SetValue(HourLabelFontSizeProperty, value);
    }

    /// <summary>
    /// Optional .NET date-time format string for the left-rail hour labels, applied to each
    /// whole hour (e.g. "H" → "23", "HH:mm" → "23:00"). Null uses the 12-hour default ("11 PM").
    /// </summary>
    public string? HourLabelFormat
    {
        get => (string?)GetValue(HourLabelFormatProperty);
        set => SetValue(HourLabelFormatProperty, value);
    }

    /// <summary>Font size of the top-row header text (e.g. day-of-week or "MON 24").</summary>
    public double HeaderPrimaryFontSize
    {
        get => (double)GetValue(HeaderPrimaryFontSizeProperty);
        set => SetValue(HeaderPrimaryFontSizeProperty, value);
    }

    /// <summary>Font size of the bottom-row header text (day number or person initials).</summary>
    public double HeaderSecondaryFontSize
    {
        get => (double)GetValue(HeaderSecondaryFontSizeProperty);
        set => SetValue(HeaderSecondaryFontSizeProperty, value);
    }

    /// <summary>Font size of an appointment block's title.</summary>
    public double BlockTitleFontSize
    {
        get => (double)GetValue(BlockTitleFontSizeProperty);
        set => SetValue(BlockTitleFontSizeProperty, value);
    }

    /// <summary>Font size of an appointment block's time range subtitle.</summary>
    public double BlockRangeFontSize
    {
        get => (double)GetValue(BlockRangeFontSizeProperty);
        set => SetValue(BlockRangeFontSizeProperty, value);
    }

    /// <summary>Width of the left time-rail column, in logical pixels.</summary>
    public double TimeRailWidth
    {
        get => (double)GetValue(TimeRailWidthProperty);
        set => SetValue(TimeRailWidthProperty, value);
    }

    /// <summary>Height reserved for the column headers, in logical pixels.</summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }
}
