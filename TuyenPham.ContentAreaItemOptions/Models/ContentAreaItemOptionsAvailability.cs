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
    /// Only content types (or ContentArea properties) with an explicit
    /// <see cref="ContentAreaItemOptionsAttribute"/> will see it.
    /// </summary>
    Specific,

    /// <summary>
    /// The selector is unconditionally hidden everywhere.
    /// <see cref="ContentAreaItemOptionsAttribute"/> cannot opt back in, and any value already
    /// stored in render settings is ignored during rendering. Use this to retire a selector
    /// without deleting its definition or its persisted data.
    /// </summary>
    None
}
