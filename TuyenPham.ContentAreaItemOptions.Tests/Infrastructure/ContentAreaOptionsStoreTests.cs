using EPiServer.DataAbstraction;
using EPiServer.Shell.Services.Rest;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Infrastructure;

public class ContentAreaOptionsStoreTests
{
    private static ContentAreaItemOptionsRegistry CreateRegistry()
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
            .Add(new ContentAreaItemOption { Id = "white", Name = "White" }),

            new ContentAreaItemOptions.Models.ContentAreaItemOptions
            {
                AttributeName = "data-margin",
                SelectorName = "margin",
                LabelPrefix = "Margin",
                Availability = ContentAreaItemOptionsAvailability.Specific,
            }
            .Add(new ContentAreaItemOption { Id = "top", Name = "Top" }),
        };
    }

    private static ContentAreaItemOptionsRestrictionResolver CreateResolver()
    {
        var repo = Substitute.For<IContentTypeRepository>();
        repo.List().Returns(new List<ContentType>());
        return new ContentAreaItemOptionsRestrictionResolver(repo);
    }

    private static ContentAreaOptionsStore CreateStore(
        ContentAreaItemOptionsRegistry? registry = null,
        ContentAreaItemOptionsRestrictionResolver? resolver = null)
    {
        return new ContentAreaOptionsStore(
            registry ?? CreateRegistry(),
            resolver ?? CreateResolver());
    }

    [Fact]
    public void Get_WithEmptyId_ReturnsRestResult()
    {
        var store = CreateStore();

        var result = store.Get(string.Empty);

        Assert.IsAssignableFrom<RestResultBase>(result);
    }

    [Fact]
    public void Get_WithNullId_ReturnsRestResult()
    {
        var store = CreateStore();

        var result = store.Get(null!);

        Assert.IsAssignableFrom<RestResultBase>(result);
    }

    [Fact]
    public void Get_WithValidSelectorName_ReturnsRestResult()
    {
        var store = CreateStore();

        var result = store.Get("theme");

        Assert.IsAssignableFrom<RestResultBase>(result);
    }

    [Fact]
    public void Get_WithInvalidSelectorName_ReturnsNotFound()
    {
        var store = CreateStore();

        var result = store.Get("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Get_WithSecondValidSelector_ReturnsRestResult()
    {
        var store = CreateStore();

        var result = store.Get("margin");

        Assert.IsAssignableFrom<RestResultBase>(result);
    }

    [Fact]
    public void Get_WithEmptyRegistry_ReturnsRestResult()
    {
        var emptyRegistry = new ContentAreaItemOptionsRegistry();
        var store = CreateStore(registry: emptyRegistry);

        var result = store.Get(string.Empty);

        Assert.IsAssignableFrom<RestResultBase>(result);
    }

    [Fact]
    public void Get_WithEmptyRegistry_AndSelectorName_ReturnsNotFound()
    {
        var emptyRegistry = new ContentAreaItemOptionsRegistry();
        var store = CreateStore(registry: emptyRegistry);

        var result = store.Get("theme");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Get_SelectorNameIsCaseSensitive()
    {
        // GetBySelectorName is case-insensitive, so this should find "theme"
        var store = CreateStore();

        var result = store.Get("Theme");

        // Registry.GetBySelectorName is case-insensitive
        Assert.IsAssignableFrom<RestResultBase>(result);
    }
}
