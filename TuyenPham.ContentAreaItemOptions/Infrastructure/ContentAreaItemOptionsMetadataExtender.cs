using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EPiServer.Core;
using EPiServer.Shell.ObjectEditing;
using EPiServer.Shell.ObjectEditing.EditorDescriptors;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Infrastructure;

/// <summary>
/// Extends the ContentArea editor metadata with per-property
/// <see cref="ContentAreaItemOptionsAttribute"/> and <see cref="HideContentAreaItemOptionsAttribute"/> overrides.
/// When a ContentArea property is decorated with these attributes,
/// the allowed selector/option pairs are passed to the client-side
/// editor via <c>EditorConfiguration["contentAreaItemOptions"]</c>.
/// </summary>
[EditorDescriptorRegistration(
    TargetType = typeof(ContentArea),
    EditorDescriptorBehavior = EditorDescriptorBehavior.PlaceLast)]
public sealed class ContentAreaItemOptionsMetadataExtender : EditorDescriptor
{
    public override void ModifyMetadata(ExtendedMetadata metadata, IEnumerable<Attribute> attributes)
    {
        base.ModifyMetadata(metadata, attributes);

        var overrides = BuildOverrides(attributes);
        if (overrides is not null)
        {
            metadata.EditorConfiguration["contentAreaItemOptions"] = overrides;
        }
    }

    /// <summary>
    /// Builds the per-selector override dictionary from the given attributes.
    /// Returns <c>null</c> when no relevant attributes are present.
    /// Use this to extract property-level overrides for
    /// <see cref="ContentAreaItemOptionsRestrictionResolver.GetApplicableCssClasses"/>
    /// and <see cref="ContentAreaItemOptionsRestrictionResolver.IsOptionApplicable"/>.
    /// </summary>
    public static Dictionary<string, string[]?>? BuildOverrides(IEnumerable<Attribute> attributes)
    {
        var opts = attributes.OfType<ContentAreaItemOptionsAttribute>().ToList();
        var hides = attributes.OfType<HideContentAreaItemOptionsAttribute>().ToList();

        if (opts.Count == 0 && hides.Count == 0)
        {
            return null;
        }

        // Structure: { "data-custom-theme": ["dark", "light"], "data-margin": [] }
        // Empty array means the selector is explicitly enabled with all options.
        // null means the selector is hidden.
        var overrides = opts.ToDictionary(
            o => o.AttributeName,
            o => (string[]?)o.AllowedOptionIds);

        foreach (var hide in hides)
        {
            overrides[hide.AttributeName] = null;
        }

        return overrides;
    }

    /// <summary>
    /// Extracts property-level overrides from <see cref="ContentAreaItemOptionsAttribute"/> and
    /// <see cref="HideContentAreaItemOptionsAttribute"/> on a specific <see cref="ContentArea"/> property.
    /// Returns <c>null</c> when the property has no relevant attributes or cannot be found.
    /// </summary>
    /// <param name="ownerType">The type that declares the ContentArea property (e.g. a page or block model type).</param>
    /// <param name="propertyName">The name of the ContentArea property.</param>
    /// <returns>A dictionary of attribute name → allowed option IDs, or <c>null</c> if no overrides apply.</returns>
    public static Dictionary<string, string[]?>? GetPropertyOverrides(Type ownerType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(ownerType);

        var property = ownerType.GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        return BuildOverrides(property.GetCustomAttributes<Attribute>(inherit: true));
    }

    /// <summary>
    /// Extracts property-level overrides from the model metadata of a <see cref="ContentArea"/> property.
    /// Intended for <c>ContentAreaRenderer.Render</c>, where
    /// <c>htmlHelper.ViewData.ModelMetadata</c> describes the ContentArea property being rendered.
    /// </summary>
    /// <param name="metadata">The model metadata of the ContentArea property.</param>
    /// <returns>A dictionary of attribute name → allowed option IDs, or <c>null</c> if no overrides apply.</returns>
    public static Dictionary<string, string[]?>? GetPropertyOverrides(ModelMetadata? metadata) =>
        metadata is { ContainerType: not null, PropertyName: not null }
            ? GetPropertyOverrides(metadata.ContainerType, metadata.PropertyName)
            : null;
}
