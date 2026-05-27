namespace Omnicasa.Schedule;

/// <summary>
/// One entry in an appointment's long-press menu: a label, an optional platform icon, and an
/// optional destructive flag. Returned from <see cref="ScheduleView.ItemActionsProvider"/>.
/// </summary>
public sealed class ScheduleMenuAction
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleMenuAction"/> class.</summary>
    /// <param name="label">The menu item text (also reported back via <c>ItemActionInvoked</c>).</param>
    /// <param name="icon">
    /// Optional platform icon name. On iOS this is an SF Symbol name (e.g. "pencil", "trash");
    /// on Android a drawable resource name (e.g. "ic_edit"). Unresolved names are simply ignored.
    /// </param>
    /// <param name="isDestructive">When true, the item is styled as destructive on iOS (red).</param>
    public ScheduleMenuAction(string label, string? icon = null, bool isDestructive = false)
    {
        Label = label;
        Icon = icon;
        IsDestructive = isDestructive;
    }

    /// <summary>Gets the menu item text.</summary>
    public string Label { get; }

    /// <summary>Gets the optional platform icon name (iOS SF Symbol / Android drawable resource).</summary>
    public string? Icon { get; }

    /// <summary>Gets a value indicating whether the item is destructive (red on iOS).</summary>
    public bool IsDestructive { get; }
}
