using System.Linq;
using EPiServer.Shell.Services.Rest;
using Microsoft.AspNetCore.Mvc;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Infrastructure;

/// <summary>
/// Optimizely REST store that exposes content area item option selectors and their
/// per-content-type restrictions to the editor UI.
/// <para>
/// <c>GET</c> with no id returns all selectors.
/// <c>GET</c> with a selector name returns that single selector's options and restrictions.
/// </para>
/// </summary>
[RestStore("content-area-options")]
[ValidateAntiForgeryToken]
public sealed class ContentAreaOptionsStore(
    ContentAreaItemOptionsRegistry registry,
    ContentAreaItemOptionsRestrictionResolver restrictionResolver)
    : RestControllerBase
{
    /// <summary>
    /// Returns all selectors when <paramref name="id"/> is empty,
    /// or a single selector matching the given <paramref name="id"/> (selector name).
    /// </summary>
    /// <param name="id">Optional selector name. Empty or <c>null</c> returns all selectors.</param>
    public IActionResult Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Rest(registry.Select(Project).ToList());
        }

        var selector = registry.GetBySelectorName(id);
        if (selector == null)
        {
            return NotFound();
        }

        return Rest(Project(selector));
    }

    // Property names are camel-cased by the shell serializer; the client reads them as-is.
    private object Project(Models.ContentAreaItemOptions selector) => new
    {
        selectorName = selector.SelectorName,
        attributeName = selector.AttributeName,
        labelPrefix = selector.LabelPrefix,
        defaultLabel = selector.DefaultLabel,
        availability = selector.Availability.ToString(),
        options = selector.ToList(),
        restrictions = restrictionResolver.GetRestrictions(selector.AttributeName),
    };
}
