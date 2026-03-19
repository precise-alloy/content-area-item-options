using System;
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
    public static IServiceCollection AddContentAreaItemOptions(
        this IServiceCollection services,
        ContentAreaItemOptionsRegistry optionsRegistry)
    {
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
}
