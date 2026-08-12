using System.Text.Json;
using System.Text.Json.Nodes;
using EPiServer.DataAbstraction;
using EPiServer.Shell.Services.Rest;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;
using ItemOptions = TuyenPham.ContentAreaItemOptions.Models.ContentAreaItemOptions;

namespace TuyenPham.ContentAreaItemOptions.Tests.Infrastructure;

/// <summary>
/// The store payload is the contract with the Dojo client, so these tests assert the
/// serialized JSON rather than just the action result type.
/// </summary>
public class ContentAreaOptionsStoreTests
{
    [ContentAreaItemOptions("data-theme", "black")]
    private class BlockWithThemeRestriction { }

    [HideContentAreaItemOptions("data-theme")]
    private class BlockWithHiddenTheme { }

    // Mirrors EPiServer.Shell's DefaultSystemTextJsonSettingsOptionsConfigurer.
    private static readonly JsonSerializerOptions ShellJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private static ContentAreaItemOptionsRegistry CreateRegistry()
    {
        return new ContentAreaItemOptionsRegistry
        {
            new ItemOptions
            {
                AttributeName = "data-theme",
                SelectorName = "theme",
                LabelPrefix = "Theme",
            }
            .Add(new ContentAreaItemOption
            {
                Id = "black",
                Name = "Black",
                Description = "Dark background",
                CssClass = "theme-black",
                IconClass = "icon-black",
            })
            .Add(new ContentAreaItemOption { Id = "white", Name = "White" }),

            new ItemOptions
            {
                AttributeName = "data-margin",
                SelectorName = "margin",
                LabelPrefix = "Margin",
                DefaultLabel = "Inherit",
                Availability = ContentAreaItemOptionsAvailability.Specific,
            }
            .Add(new ContentAreaItemOption { Id = "top", Name = "Top" }),
        };
    }

    private static ContentAreaItemOptionsRestrictionResolver CreateResolver(
        params (int id, Type modelType)[] contentTypes)
    {
        var repo = Substitute.For<IContentTypeRepository>();
        repo.List().Returns(contentTypes
            .Select(ct =>
            {
                var contentType = new ContentType { ID = ct.id };
                contentType.ModelType = ct.modelType;
                return contentType;
            })
            .ToList());

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

    private static JsonNode Serialize(IActionResult result)
    {
        var data = Assert.IsType<RestResult>(result).Data;
        return JsonSerializer.SerializeToNode(data, ShellJsonOptions)!;
    }

    // --- Payload shape ---

    [Fact]
    public void Get_WithEmptyId_ReturnsEverySelector()
    {
        var json = Serialize(CreateStore().Get(string.Empty)).AsArray();

        Assert.Equal(2, json.Count);
        Assert.Equal("theme", (string?)json[0]!["selectorName"]);
        Assert.Equal("margin", (string?)json[1]!["selectorName"]);
    }

    [Fact]
    public void Get_WithNullId_ReturnsEverySelector()
    {
        Assert.Equal(2, Serialize(CreateStore().Get(null!)).AsArray().Count);
    }

    [Fact]
    public void Get_ProjectsEverySelectorFieldTheClientReads()
    {
        var selector = Serialize(CreateStore().Get(string.Empty)).AsArray()[0]!;

        Assert.Equal("theme", (string?)selector["selectorName"]);
        Assert.Equal("data-theme", (string?)selector["attributeName"]);
        Assert.Equal("Theme", (string?)selector["labelPrefix"]);
        Assert.Equal("Default", (string?)selector["defaultLabel"]);
        Assert.Equal("All", (string?)selector["availability"]);
        Assert.NotNull(selector["options"]);
        Assert.NotNull(selector["restrictions"]);
    }

    [Fact]
    public void Get_ProjectsEveryOptionFieldTheClientReads()
    {
        var option = Serialize(CreateStore().Get("theme")).AsObject()["options"]!.AsArray()[0]!;

        Assert.Equal("black", (string?)option["id"]);
        Assert.Equal("Black", (string?)option["name"]);
        Assert.Equal("Dark background", (string?)option["description"]);
        Assert.Equal("theme-black", (string?)option["cssClass"]);
        Assert.Equal("icon-black", (string?)option["iconClass"]);
    }

    [Fact]
    public void Get_PreservesOptionalOptionFieldsAsNull()
    {
        var option = Serialize(CreateStore().Get("theme")).AsObject()["options"]!.AsArray()[1]!;

        Assert.Equal("white", (string?)option["id"]);
        Assert.Null((string?)option["description"]);
        Assert.Null((string?)option["cssClass"]);
        Assert.Null((string?)option["iconClass"]);
    }

    [Fact]
    public void Get_SerializesAvailabilityAsName()
    {
        var selectors = Serialize(CreateStore().Get(string.Empty)).AsArray();

        Assert.Equal("Specific", (string?)selectors[1]!["availability"]);
    }

    [Fact]
    public void Get_SerializesCustomDefaultLabel()
    {
        var selectors = Serialize(CreateStore().Get(string.Empty)).AsArray();

        Assert.Equal("Inherit", (string?)selectors[1]!["defaultLabel"]);
    }

    [Fact]
    public void Get_DoesNotCamelCaseAttributeNameKeys()
    {
        var restrictions = Serialize(
                CreateStore(resolver: CreateResolver((7, typeof(BlockWithThemeRestriction)))).Get("theme"))
            .AsObject()["restrictions"]!
            .AsObject();

        // The client looks up restrictions by content type id.
        Assert.True(restrictions.ContainsKey("7"));
    }

    // --- Restrictions ---

    [Fact]
    public void Get_IncludesAllowedOptionIds_ForRestrictedContentType()
    {
        var store = CreateStore(resolver: CreateResolver((7, typeof(BlockWithThemeRestriction))));

        var restrictions = Serialize(store.Get("theme")).AsObject()["restrictions"]!.AsObject();

        Assert.Equal(["black"], restrictions["7"]!.AsArray().Select(n => (string?)n));
    }

    [Fact]
    public void Get_IncludesNullRestriction_ForHiddenContentType()
    {
        var store = CreateStore(resolver: CreateResolver((9, typeof(BlockWithHiddenTheme))));

        var restrictions = Serialize(store.Get("theme")).AsObject()["restrictions"]!.AsObject();

        // null must survive serialization; the client treats it as "hidden".
        Assert.True(restrictions.ContainsKey("9"));
        Assert.Null(restrictions["9"]);
    }

    [Fact]
    public void Get_ReturnsEmptyRestrictions_WhenNoContentTypeDeclaresAttributes()
    {
        var restrictions = Serialize(CreateStore().Get("theme")).AsObject()["restrictions"]!.AsObject();

        Assert.Empty(restrictions);
    }

    // --- Single selector lookup ---

    [Fact]
    public void Get_WithValidSelectorName_ReturnsSameShapeAsTheListEntry()
    {
        var single = Serialize(CreateStore().Get("theme")).AsObject();
        var fromList = Serialize(CreateStore().Get(string.Empty)).AsArray()[0]!.AsObject();

        Assert.Equal(fromList.ToJsonString(), single.ToJsonString());
    }

    [Fact]
    public void Get_SelectorNameLookupIsCaseInsensitive()
    {
        var json = Serialize(CreateStore().Get("Theme")).AsObject();

        Assert.Equal("theme", (string?)json["selectorName"]);
    }

    [Fact]
    public void Get_WithUnknownSelectorName_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(CreateStore().Get("nonexistent"));
    }

    [Fact]
    public void Get_WithEmptyRegistry_ReturnsEmptyList()
    {
        var store = CreateStore(registry: []);

        Assert.Empty(Serialize(store.Get(string.Empty)).AsArray());
    }

    [Fact]
    public void Get_WithEmptyRegistry_AndSelectorName_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(CreateStore(registry: []).Get("theme"));
    }
}
