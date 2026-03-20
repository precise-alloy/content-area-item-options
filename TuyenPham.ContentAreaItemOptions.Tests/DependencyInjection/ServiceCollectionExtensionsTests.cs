using EPiServer.Shell.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TuyenPham.ContentAreaItemOptions.DependencyInjection;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    private static ContentAreaItemOptionsRegistry CreateValidRegistry()
    {
        return new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-theme",
                SelectorName = "theme",
                LabelPrefix = "Theme",
            }
            .Add(new ContentAreaItemOption { Id = "black", Name = "Black" })
        };
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersRegistry_AsSingleton()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        services.AddContentAreaItemOptions(registry);

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ContentAreaItemOptionsRegistry));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersSameRegistryInstance()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        services.AddContentAreaItemOptions(registry);

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetService<ContentAreaItemOptionsRegistry>();
        Assert.Same(registry, resolved);
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersRestrictionResolver_AsSingleton()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        services.AddContentAreaItemOptions(registry);

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ContentAreaItemOptionsRestrictionResolver));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersModuleInProtectedModuleOptions()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        services.AddContentAreaItemOptions(registry);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ProtectedModuleOptions>>().Value;
        Assert.Contains(options.Items,
            item => item.Name == "TuyenPham.ContentAreaItemOptions");
    }

    [Fact]
    public void AddContentAreaItemOptions_DoesNotDuplicateModule_OnMultipleCalls()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        services.AddContentAreaItemOptions(registry);
        services.AddContentAreaItemOptions(registry);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ProtectedModuleOptions>>().Value;

        var count = options.Items.Count(i =>
            i.Name.Equals("TuyenPham.ContentAreaItemOptions", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, count);
    }

    [Fact]
    public void AddContentAreaItemOptions_Throws_WhenAttributeNameLacksDataPrefix()
    {
        var services = new ServiceCollection();
        var registry = new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "invalid-name",
                SelectorName = "test",
                LabelPrefix = "Test",
            }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => services.AddContentAreaItemOptions(registry));

        Assert.Contains("data-", ex.Message);
        Assert.Contains("invalid-name", ex.Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_Throws_ListingAllInvalidNames()
    {
        var services = new ServiceCollection();
        var registry = new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "bad-one",
                SelectorName = "one",
                LabelPrefix = "One",
            },
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-good",
                SelectorName = "good",
                LabelPrefix = "Good",
            },
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "bad-two",
                SelectorName = "two",
                LabelPrefix = "Two",
            }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => services.AddContentAreaItemOptions(registry));

        Assert.Contains("bad-one", ex.Message);
        Assert.Contains("bad-two", ex.Message);
        Assert.DoesNotContain("data-good", ex.Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_AcceptsDataPrefix_CaseInsensitive()
    {
        var services = new ServiceCollection();
        var registry = new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "DATA-Theme",
                SelectorName = "theme",
                LabelPrefix = "Theme",
            }
        };

        // Should not throw
        services.AddContentAreaItemOptions(registry);
    }

    [Fact]
    public void AddContentAreaItemOptions_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        var registry = CreateValidRegistry();

        var result = services.AddContentAreaItemOptions(registry);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddContentAreaItemOptions_ValidRegistryWithMultipleSelectors_Succeeds()
    {
        var services = new ServiceCollection();
        var registry = new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-theme",
                SelectorName = "theme",
                LabelPrefix = "Theme",
            },
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-margin",
                SelectorName = "margin",
                LabelPrefix = "Margin",
            },
            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-padding",
                SelectorName = "padding",
                LabelPrefix = "Padding",
            }
        };

        // Should not throw
        services.AddContentAreaItemOptions(registry);

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetService<ContentAreaItemOptionsRegistry>();
        Assert.NotNull(resolved);
        Assert.Equal(3, resolved.Count());
    }
}
