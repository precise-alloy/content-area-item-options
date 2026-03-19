using System;

namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Restrict available options for a content area item selector on a specific block type.
/// <list type="bullet">
/// <item>With option IDs: only those options are shown.</item>
/// <item>Without option IDs: the selector is hidden for this block type.</item>
/// <item>No attribute: all options are shown (default).</item>
/// </list>
/// </summary>
/// <remarks>
/// Restricts available options for a content area item selector on this block type.
/// </remarks>
/// <param name="attributeName">The <see cref="ContentAreaItemOptions.AttributeName"/> of the target selector.</param>
/// <param name="allowedOptionIds">
/// The option IDs to allow. Pass specific IDs to restrict, or omit to hide the selector entirely.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
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
    /// Empty array means the selector is hidden for this content type.
    /// </summary>
    public string[] AllowedOptionIds { get; } = allowedOptionIds;
}