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
[ValidateAntiForgeryReleaseToken]
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
            var selectors = registry.Select(s => new
            {
                selectorName = s.SelectorName,
                attributeName = s.AttributeName,
                labelPrefix = s.LabelPrefix,
                defaultLabel = s.DefaultLabel,
                availability = s.Availability.ToString(),
                options = s.ToList(),
                restrictions = restrictionResolver.GetRestrictions(s.AttributeName),
            });

            return Rest(selectors);
        }

        var selector = registry.GetBySelectorName(id);
        if (selector == null)
        {
            return NotFound();
        }

        return Rest(new
        {
            options = selector.ToList(),
            restrictions = restrictionResolver.GetRestrictions(selector.AttributeName),
        });
    }
}
