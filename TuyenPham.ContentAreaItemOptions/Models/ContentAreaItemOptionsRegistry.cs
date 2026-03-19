using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// A collection of <see cref="ContentAreaItemOptions"/> selectors.
/// Register a single instance via <see cref="DependencyInjection.ServiceCollectionExtensions.AddContentAreaItemOptions"/>.
/// Supports collection-initializer syntax.
/// </summary>
public sealed class ContentAreaItemOptionsRegistry : IEnumerable<ContentAreaItemOptions>
{
    private readonly List<ContentAreaItemOptions> _selectors = [];

    /// <summary>
    /// Adds a selector to the registry.
    /// </summary>
    /// <param name="selector">The selector definition to register.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public ContentAreaItemOptionsRegistry Add(ContentAreaItemOptions selector)
    {
        _selectors.Add(selector);
        return this;
    }

    /// <summary>
    /// Finds a selector by its <see cref="ContentAreaItemOptions.AttributeName"/> (case-insensitive).
    /// </summary>
    /// <param name="attributeName">The attribute name to look up.</param>
    /// <returns>The matching selector, or <c>null</c> if not found.</returns>
    public ContentAreaItemOptions? GetByAttributeName(string attributeName) =>
        _selectors.FirstOrDefault(s => string.Equals(s.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds a selector by its <see cref="ContentAreaItemOptions.SelectorName"/> (case-insensitive).
    /// </summary>
    /// <param name="selectorName">The selector name to look up.</param>
    /// <returns>The matching selector, or <c>null</c> if not found.</returns>
    public ContentAreaItemOptions? GetBySelectorName(string selectorName) =>
        _selectors.FirstOrDefault(s => string.Equals(s.SelectorName, selectorName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public IEnumerator<ContentAreaItemOptions> GetEnumerator() => _selectors.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}