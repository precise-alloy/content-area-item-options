namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Represents a single selectable option within a content area item selector.
/// </summary>
public sealed class ContentAreaItemOption
{
    /// <summary>
    /// Unique identifier for the option. Stored in <c>ContentAreaItem.RenderSettings</c> when selected.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Display name shown to editors in the selector menu.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description or tooltip displayed in the selector UI.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional CSS class to apply during rendering. How this is used depends on your <c>ContentAreaRenderer</c> implementation.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Optional CSS class for an icon displayed next to the option in the selector UI.
    /// </summary>
    public string? IconClass { get; set; }
}
