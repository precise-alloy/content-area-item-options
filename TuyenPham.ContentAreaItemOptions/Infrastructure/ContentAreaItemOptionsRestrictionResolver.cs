using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EPiServer.DataAbstraction;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Infrastructure;

/// <summary>
/// Scans all registered content types for <see cref="ContentAreaItemOptionsAttribute"/> and
/// <see cref="HideContentAreaItemOptionsAttribute"/> declarations and builds a lazy-loaded
/// restriction map used by the REST store and client-side UI to determine which options
/// are available per content type.
/// </summary>
public sealed class ContentAreaItemOptionsRestrictionResolver(
    IContentTypeRepository contentTypeRepository)
{
    private readonly Lazy<Dictionary<string, Dictionary<int, string[]?>>> _restrictions = new(() =>
    {
        var result = new Dictionary<string, Dictionary<int, string[]?>>();

        foreach (var contentType in contentTypeRepository.List())
        {
            if (contentType.ModelType is null)
            {
                continue;
            }

            var filters = contentType.ModelType
                .GetCustomAttributes<ContentAreaItemOptionsAttribute>()
                .ToList();

            foreach (var filter in filters)
            {
                if (!result.TryGetValue(filter.AttributeName, out var map))
                {
                    map = new Dictionary<int, string[]?>();
                    result[filter.AttributeName] = map;
                }

                map[contentType.ID] = filter.AllowedOptionIds;
            }

            var hides = contentType.ModelType
                .GetCustomAttributes<HideContentAreaItemOptionsAttribute>()
                .ToList();

            foreach (var hide in hides)
            {
                if (!result.TryGetValue(hide.AttributeName, out var map))
                {
                    map = new Dictionary<int, string[]?>();
                    result[hide.AttributeName] = map;
                }

                map[contentType.ID] = null;
            }
        }

        return result;
    });

    /// <summary>
    /// Returns content type ID → allowed option IDs for a given selector attribute name.
    /// Only content types with explicit attributes are included.
    /// Missing types = falls back to <see cref="ContentAreaItemOptions.Availability"/> setting.
    /// Empty array value = all options enabled.
    /// <c>null</c> value = selector hidden.
    /// </summary>
    public Dictionary<int, string[]?> GetRestrictions(string attributeName)
    {
        return _restrictions.Value.TryGetValue(attributeName, out var map)
            ? map
            : new Dictionary<int, string[]?>();
    }

    /// <summary>
    /// Determines whether a specific option is applicable for a content type,
    /// taking into account <see cref="ContentAreaItemOptionsAttribute"/>,
    /// <see cref="HideContentAreaItemOptionsAttribute"/>, and the selector's
    /// <see cref="ContentAreaItemOptions.Availability"/> setting.
    /// </summary>
    /// <param name="selector">The selector that owns the option.</param>
    /// <param name="optionId">The option identifier to check.</param>
    /// <param name="contentTypeId">The content type ID of the block being rendered.</param>
    /// <returns><c>true</c> if the option should be applied; <c>false</c> if it has been hidden or restricted.</returns>
    public bool IsOptionApplicable(Models.ContentAreaItemOptions selector, string optionId, int contentTypeId)
    {
        var restrictions = GetRestrictions(selector.AttributeName);

        if (restrictions.TryGetValue(contentTypeId, out var allowedIds))
        {
            // null = selector is hidden for this content type
            if (allowedIds is null)
            {
                return false;
            }

            // Non-empty = only these option IDs are allowed
            if (allowedIds.Length > 0
                && !allowedIds.Contains(optionId, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        else if (selector.Availability == ContentAreaItemOptionsAvailability.Specific)
        {
            // Specific mode: hidden unless content type explicitly opts in
            return false;
        }

        return true;
    }

    /// <summary>
    /// Collects CSS classes from applicable options in the render settings.
    /// Options that have been hidden or restricted for the given content type are skipped.
    /// </summary>
    /// <param name="registry">The registry containing all selectors.</param>
    /// <param name="renderSettings">The content area item's render settings.</param>
    /// <param name="contentTypeId">
    /// The content type ID of the block being rendered.
    /// When <c>null</c>, restriction checks are skipped and all stored options are returned.
    /// </param>
    /// <returns>A space-separated string of CSS classes from applicable options.</returns>
    public string GetApplicableCssClasses(
        ContentAreaItemOptionsRegistry registry,
        IDictionary<string, object> renderSettings,
        int? contentTypeId)
    {
        var classes = new List<string>();

        foreach (var selector in registry)
        {
            if (!renderSettings.TryGetValue(selector.AttributeName, out var value)
                || value is not string id)
            {
                continue;
            }

            // Skip options that are no longer applicable for this content type
            if (contentTypeId.HasValue
                && !IsOptionApplicable(selector, id, contentTypeId.Value))
            {
                continue;
            }

            if (selector.Get(id) is { CssClass: not null } option)
            {
                classes.Add(option.CssClass);
            }
        }

        return string.Join(" ", classes);
    }
}
