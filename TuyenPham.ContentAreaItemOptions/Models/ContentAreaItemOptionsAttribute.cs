using System;

namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Enables or restricts available options for a content area item selector on a specific block type or content area property.
/// <list type="bullet">
/// <item>With option IDs: only those options are shown.</item>
/// <item>Without option IDs: all options are enabled (useful with <see cref="ContentAreaItemOptionsAvailability.Specific"/> to opt in).</item>
/// <item>No attribute: falls back to <see cref="ContentAreaItemOptions.Availability"/> setting.</item>
/// </list>
/// To hide a selector, use <see cref="HideContentAreaItemOptionsAttribute"/> instead.
/// </summary>
/// <remarks>
/// Enables or restricts available options for a content area item selector.
/// </remarks>
/// <param name="attributeName">The <see cref="ContentAreaItemOptions.AttributeName"/> of the target selector.</param>
/// <param name="allowedOptionIds">
/// The option IDs to allow. Pass specific IDs to restrict, or omit to enable all options.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public sealed class ContentAreaItemOptionsAttribute(
    string attributeName,
    params string[] allowedOptionIds) : Attribute
{
    /// <summary>
    /// The <see cref="ContentAreaItemOptions.AttributeName"/> of the selector this restriction applies to.
    /// </summary>
    public string AttributeName { get; } = attributeName;

    /// <summary>
    /// The option IDs that are allowed for this content type.
    /// Null or empty array means all options are enabled.
    /// </summary>
    public string[] AllowedOptionIds { get; } = allowedOptionIds ?? [];
}