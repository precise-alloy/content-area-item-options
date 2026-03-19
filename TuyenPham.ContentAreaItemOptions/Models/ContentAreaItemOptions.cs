using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TuyenPham.ContentAreaItemOptions.Models;

/// <summary>
/// Defines a selector that appears in the content area item context menu,
/// containing a collection of <see cref="ContentAreaItemOption"/> choices.
/// </summary>
public sealed class ContentAreaItemOptions : IEnumerable<ContentAreaItemOption>
{
    /// <summary>
    /// The render setting key (must start with <c>data-</c>).
    /// Used to store and retrieve the selected value in <c>ContentAreaItem.RenderSettings</c>.
    /// </summary>
    public required string AttributeName { get; set; }

    /// <summary>
    /// A unique identifier for this selector. Also used as the REST store item id.
    /// </summary>
    public required string SelectorName { get; set; }

    /// <summary>
    /// Label prefix shown in the editor context menu (e.g. <c>"Theme"</c> → displays <c>"Theme: Blue"</c>).
    /// </summary>
    public required string LabelPrefix { get; set; }

    /// <summary>
    /// Label shown when no option is selected. Defaults to <c>"Default"</c>.
    /// </summary>
    public string DefaultLabel { get; set; } = "Default";

    /// <summary>
    /// Controls default visibility of this selector across content types.
    /// <see cref="ContentAreaItemOptionsAvailability.All"/>: shown for all content types unless restricted by attribute.
    /// <see cref="ContentAreaItemOptionsAvailability.Specific"/>: hidden unless a content type explicitly opts in via
    /// <see cref="ContentAreaItemOptionsAttribute"/>.
    /// </summary>
    public ContentAreaItemOptionsAvailability Availability { get; set; } = ContentAreaItemOptionsAvailability.All;

    private readonly List<ContentAreaItemOption> _options = [];

    /// <summary>
    /// Adds an option to this selector.
    /// </summary>
    /// <param name="option">The option to add.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public ContentAreaItemOptions Add(ContentAreaItemOption option)
    {
        _options.Add(option);
        return this;
    }

    /// <summary>
    /// Finds an option by its <see cref="ContentAreaItemOption.Id"/> (case-insensitive).
    /// </summary>
    /// <param name="id">The option identifier to look up.</param>
    /// <returns>The matching option, or <c>null</c> if not found.</returns>
    public ContentAreaItemOption? Get(string id) =>
        _options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public IEnumerator<ContentAreaItemOption> GetEnumerator() => _options.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
