using EPiServer.Shell.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TuyenPham.ContentAreaItemOptions.DependencyInjection;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;
using ItemOptions = TuyenPham.ContentAreaItemOptions.Models.ContentAreaItemOptions;

namespace TuyenPham.ContentAreaItemOptions.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    private static ItemOptions CreateSelector(
        string attributeName = "data-theme",
        string selectorName = "theme",
        params string[] optionIds)
    {
        var selector = new ItemOptions
        {
            AttributeName = attributeName,
            SelectorName = selectorName,
            LabelPrefix = "Theme",
        };

        foreach (var id in optionIds)
        {
            selector.Add(new ContentAreaItemOption { Id = id, Name = id });
        }

        return selector;
    }

    private static ArgumentException AssertRejects(ContentAreaItemOptionsRegistry registry) =>
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddContentAreaItemOptions(registry));

    // --- Registration ---

    [Fact]
    public void AddContentAreaItemOptions_RegistersRegistryInstance()
    {
        var registry = new ContentAreaItemOptionsRegistry { CreateSelector() };

        var provider = new ServiceCollection()
            .AddContentAreaItemOptions(registry)
            .BuildServiceProvider();

        Assert.Same(registry, provider.GetRequiredService<ContentAreaItemOptionsRegistry>());
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersRestrictionResolverAsSingleton()
    {
        var services = new ServiceCollection()
            .AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry { CreateSelector() });

        var descriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(ContentAreaItemOptionsRestrictionResolver));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddContentAreaItemOptions_RegistersProtectedModule()
    {
        var provider = new ServiceCollection()
            .AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry { CreateSelector() })
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ProtectedModuleOptions>>().Value;

        Assert.Contains(options.Items, i => i.Name == "TuyenPham.ContentAreaItemOptions");
    }

    [Fact]
    public void AddContentAreaItemOptions_DoesNotRegisterModuleTwice()
    {
        var provider = new ServiceCollection()
            .AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry { CreateSelector() })
            .AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry { CreateSelector() })
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ProtectedModuleOptions>>().Value;

        Assert.Single(options.Items, i => i.Name == "TuyenPham.ContentAreaItemOptions");
    }

    [Fact]
    public void AddContentAreaItemOptions_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry()));
    }

    [Fact]
    public void AddContentAreaItemOptions_AcceptsEmptyRegistry()
    {
        var provider = new ServiceCollection()
            .AddContentAreaItemOptions(new ContentAreaItemOptionsRegistry())
            .BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<ContentAreaItemOptionsRegistry>());
    }

    [Fact]
    public void AddContentAreaItemOptions_ThrowsArgumentNullException_WhenRegistryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddContentAreaItemOptions(null!));
    }

    // --- AttributeName validation ---

    [Theory]
    [InlineData("data-theme")]
    [InlineData("DATA-THEME")]
    [InlineData("data-")]
    public void AddContentAreaItemOptions_AcceptsDataPrefixedAttributeNames(string attributeName)
    {
        var registry = new ContentAreaItemOptionsRegistry { CreateSelector(attributeName) };

        new ServiceCollection().AddContentAreaItemOptions(registry);
    }

    [Theory]
    [InlineData("theme")]
    [InlineData("custom-theme")]
    [InlineData("dat-theme")]
    public void AddContentAreaItemOptions_RejectsAttributeNamesWithoutDataPrefix(string attributeName)
    {
        var registry = new ContentAreaItemOptionsRegistry { CreateSelector(attributeName) };

        Assert.Contains("must start with 'data-'", AssertRejects(registry).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_RejectsEmptyAttributeName()
    {
        var registry = new ContentAreaItemOptionsRegistry { CreateSelector(attributeName: "  ") };

        Assert.Contains("empty AttributeName", AssertRejects(registry).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_ReportsEveryInvalidAttributeNameAtOnce()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("theme", "theme"),
            CreateSelector("margin", "margin"),
        };

        var message = AssertRejects(registry).Message;

        Assert.Contains("'theme'", message);
        Assert.Contains("'margin'", message);
    }

    // --- Uniqueness validation ---

    [Fact]
    public void AddContentAreaItemOptions_RejectsDuplicateAttributeNames()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", "theme"),
            CreateSelector("DATA-THEME", "colour"),
        };

        Assert.Contains("Duplicate AttributeName", AssertRejects(registry).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_RejectsDuplicateSelectorNames()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", "theme"),
            CreateSelector("data-colour", "Theme"),
        };

        Assert.Contains("Duplicate SelectorName", AssertRejects(registry).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_RejectsEmptySelectorName()
    {
        var registry = new ContentAreaItemOptionsRegistry { CreateSelector(selectorName: "") };

        Assert.Contains("empty SelectorName", AssertRejects(registry).Message);
    }

    // --- Option validation ---

    [Fact]
    public void AddContentAreaItemOptions_RejectsDuplicateOptionIds()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", "theme", "black", "BLACK"),
        };

        Assert.Contains("duplicate option Id 'black'", AssertRejects(registry).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_RejectsEmptyOptionId()
    {
        var selector = CreateSelector();
        selector.Add(new ContentAreaItemOption { Id = null, Name = "Nameless" });

        Assert.Contains("empty Id", AssertRejects([selector]).Message);
    }

    [Fact]
    public void AddContentAreaItemOptions_AcceptsSameOptionIdAcrossDifferentSelectors()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", "theme", "none"),
            CreateSelector("data-margin", "margin", "none"),
        };

        new ServiceCollection().AddContentAreaItemOptions(registry);
    }
}
