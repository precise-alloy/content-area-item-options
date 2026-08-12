using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.Shell.Modules;
using Microsoft.Extensions.DependencyInjection;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.DependencyInjection;

/// <summary>
/// Extension methods for registering content area item options in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the content area item options module, including the option registry,
    /// restriction resolver, and the protected module entry so the CMS discovers
    /// the client-side resources and REST store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsRegistry">The registry containing all selector definitions.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// The registry contains selectors that would collide or that the CMS cannot persist.
    /// All problems are reported at once.
    /// </exception>
    public static IServiceCollection AddContentAreaItemOptions(
        this IServiceCollection services,
        ContentAreaItemOptionsRegistry optionsRegistry)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsRegistry);

        Validate(optionsRegistry);

        services.Configure<ProtectedModuleOptions>(o =>
        {
            const string moduleName = "TuyenPham.ContentAreaItemOptions";
            if (!o.Items.Any(i => i.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase)))
            {
                o.Items.Add(new ModuleDetails { Name = moduleName });
            }
        });
        services.AddSingleton<ContentAreaItemOptionsRestrictionResolver>();
        services.AddSingleton(optionsRegistry);

        return services;
    }

    private static void Validate(ContentAreaItemOptionsRegistry optionsRegistry)
    {
        var errors = new List<string>();
        var selectors = optionsRegistry.ToList();

        foreach (var selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector.AttributeName))
            {
                errors.Add("A selector has an empty AttributeName.");
            }
            else if (!selector.AttributeName.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
            {
                // The CMS only persists render settings whose key starts with "data-".
                errors.Add($"AttributeName '{selector.AttributeName}' must start with 'data-'.");
            }

            if (string.IsNullOrWhiteSpace(selector.SelectorName))
            {
                errors.Add($"Selector '{selector.AttributeName}' has an empty SelectorName.");
            }

            var optionIds = selector.Select(o => o.Id).ToList();

            if (optionIds.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add($"Selector '{selector.AttributeName}' has an option with an empty Id.");
            }

            var duplicateOptionIds = optionIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicate in duplicateOptionIds)
            {
                errors.Add($"Selector '{selector.AttributeName}' has duplicate option Id '{duplicate}'.");
            }
        }

        errors.AddRange(FindDuplicates(selectors.Select(s => s.AttributeName), nameof(Models.ContentAreaItemOptions.AttributeName)));
        errors.AddRange(FindDuplicates(selectors.Select(s => s.SelectorName), nameof(Models.ContentAreaItemOptions.SelectorName)));

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"Invalid ContentAreaItemOptions registry:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}",
                nameof(optionsRegistry));
        }
    }

    private static IEnumerable<string> FindDuplicates(IEnumerable<string> values, string propertyName) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate {propertyName} '{g.Key}'. Each selector must be uniquely addressable.");
}
