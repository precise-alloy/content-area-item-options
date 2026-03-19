namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Controls the default visibility of a selector across content types.
/// </summary>
public enum ContentAreaItemOptionsAvailability
{
    /// <summary>
    /// The selector is shown for all content types by default.
    /// Use <see cref="ContentAreaItemOptionsAttribute"/> to restrict or hide it on specific types.
    /// </summary>
    All,

    /// <summary>
    /// The selector is hidden by default.
    /// Only content types with an explicit <see cref="ContentAreaItemOptionsAttribute"/> will see it.
    /// </summary>
    Specific,

    /// <summary>
    /// The selector is unconditionally hidden for all content types.
    /// </summary>
    None
}
