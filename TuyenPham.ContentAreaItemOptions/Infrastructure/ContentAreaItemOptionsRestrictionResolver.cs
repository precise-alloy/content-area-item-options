using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EPiServer.DataAbstraction;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Infrastructure;

/// <summary>
/// Scans all registered content types for <see cref="ContentAreaItemOptionsAttribute"/> declarations
/// and builds a lazy-loaded restriction map used by the REST store and client-side UI
/// to determine which options are available per content type.
/// </summary>
public sealed class ContentAreaItemOptionsRestrictionResolver(
    IContentTypeRepository contentTypeRepository)
{
    private readonly Lazy<Dictionary<string, Dictionary<int, string[]>>> _restrictions = new(() =>
    {
        var result = new Dictionary<string, Dictionary<int, string[]>>();

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
                    map = new Dictionary<int, string[]>();
                    result[filter.AttributeName] = map;
                }

                map[contentType.ID] = filter.AllowedOptionIds;
            }
        }

        return result;
    });

    /// <summary>
    /// Returns content type ID → allowed option IDs for a given selector attribute name.
    /// Only content types with explicit <see cref="ContentAreaItemOptionsAttribute"/> are included.
    /// Missing types = all options allowed.
    /// Empty array value = selector hidden.
    /// </summary>
    public Dictionary<int, string[]> GetRestrictions(string attributeName)
    {
        return _restrictions.Value.TryGetValue(attributeName, out var map)
            ? map
            : new Dictionary<int, string[]>();
    }
}
