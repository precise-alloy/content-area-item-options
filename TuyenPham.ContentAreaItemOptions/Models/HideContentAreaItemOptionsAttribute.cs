using System;

namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Hides a content area item option selector for a specific block type or content area property.
/// </summary>
/// <param name="attributeName">The <see cref="ContentAreaItemOptions.AttributeName"/> of the selector to hide.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public sealed class HideContentAreaItemOptionsAttribute(string attributeName) : Attribute
{
    /// <summary>
    /// The <see cref="ContentAreaItemOptions.AttributeName"/> of the selector to hide.
    /// </summary>
    public string AttributeName { get; } = attributeName;
}
