using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omnicasa.Schedule.Widget;

/// <summary>
/// One appointment as serialized into the shared widget JSON by the app. Property names match the
/// existing agenda-widget contract (<c>Widgets/widget_appointments.json</c>) so this is drop-in.
/// </summary>
public sealed class WidgetAppointment
{
    /// <summary>Date format used in the shared JSON (<c>StartTime</c>/<c>EndTime</c>).</summary>
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Gets or sets the stable appointment id.</summary>
    [JsonPropertyName("AppointmentId")]
    public long AppointmentId { get; set; }

    /// <summary>Gets or sets the title shown on the block.</summary>
    [JsonPropertyName("Title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the optional subtitle (e.g. place).</summary>
    [JsonPropertyName("SubTitle")]
    public string? SubTitle { get; set; }

    /// <summary>Gets or sets the start time string (<see cref="DateFormat"/>).</summary>
    [JsonPropertyName("StartTime")]
    public string? StartTime { get; set; }

    /// <summary>Gets or sets the end time string (<see cref="DateFormat"/>).</summary>
    [JsonPropertyName("EndTime")]
    public string? EndTime { get; set; }

    /// <summary>Gets or sets the block color as hex (<c>#RRGGBB</c>).</summary>
    [JsonPropertyName("BackgroundColor")]
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the optional place text.</summary>
    [JsonPropertyName("Place")]
    public string? Place { get; set; }

    /// <summary>Gets or sets the optional status image key.</summary>
    [JsonPropertyName("ImageKey")]
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a super appointment.</summary>
    [JsonPropertyName("IsSuperAppointment")]
    public bool IsSuperAppointment { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a group appointment.</summary>
    [JsonPropertyName("IsGroupAppointment")]
    public bool IsGroupAppointment { get; set; }

    /// <summary>Gets or sets a value indicating whether this is private.</summary>
    [JsonPropertyName("IsPrivate")]
    public bool IsPrivate { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a recurrence.</summary>
    [JsonPropertyName("IsRecurrence")]
    public bool IsRecurrence { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a recurrence exception.</summary>
    [JsonPropertyName("IsRecurrenceException")]
    public bool IsRecurrenceException { get; set; }

    /// <summary>Gets or sets a value indicating whether this has a comment.</summary>
    [JsonPropertyName("IsCommented")]
    public bool IsCommented { get; set; }

    /// <summary>Gets the parsed start, or null when the string is missing/malformed.</summary>
    public DateTime? Start => Parse(StartTime);

    /// <summary>Gets the parsed end, or null when the string is missing/malformed.</summary>
    public DateTime? End => Parse(EndTime);

    private static DateTime? Parse(string? value)
        => DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
}

/// <summary>The shared-file wrapper the app writes: <c>{ "Time": ..., "Appointments": [...] }</c>.</summary>
public sealed class WidgetAppointmentShare
{
    /// <summary>Gets or sets the snapshot time string.</summary>
    [JsonPropertyName("Time")]
    public string? Time { get; set; }

    /// <summary>Gets or sets the appointments in the snapshot.</summary>
    [JsonPropertyName("Appointments")]
    public List<WidgetAppointment> Appointments { get; set; } = new List<WidgetAppointment>();

    /// <summary>Deserializes a share payload; returns an empty list on any failure.</summary>
    /// <param name="json">The shared JSON.</param>
    /// <returns>The parsed appointments, or an empty list.</returns>
    public static IReadOnlyList<WidgetAppointment> Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<WidgetAppointmentShare>(json)?.Appointments
                   ?? new List<WidgetAppointment>();
        }
        catch (JsonException)
        {
            return new List<WidgetAppointment>();
        }
    }
}
