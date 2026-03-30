using EPiServer.DataAbstraction;
using NSubstitute;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;
using ItemOptions = TuyenPham.ContentAreaItemOptions.Models.ContentAreaItemOptions;

namespace TuyenPham.ContentAreaItemOptions.Tests.Infrastructure;

public class ContentAreaItemOptionsRestrictionResolverTests
{
    // --- Test model types with attributes ---

    [ContentAreaItemOptions("data-theme", "black", "white")]
    private class BlockWithThemeRestriction { }

    [HideContentAreaItemOptions("data-margin")]
    private class BlockWithHiddenMargin { }

    [ContentAreaItemOptions("data-theme")]
    [ContentAreaItemOptions("data-layout", "wide")]
    private class BlockWithMultipleAttributes { }

    private class BlockWithNoAttributes { }

    [ContentAreaItemOptions("data-theme", "black")]
    [HideContentAreaItemOptions("data-theme")]
    private class BlockWithConflictingAttributes { }

    [ContentAreaItemOptions("data-theme", "red", "green")]
    [HideContentAreaItemOptions("data-margin")]
    private class BlockWithMixedAttributes { }

    // --- Helper ---

    private static ContentType CreateContentType(int id, Type? modelType)
    {
        var ct = new ContentType { ID = id };
        if (modelType is not null)
        {
            ct.ModelType = modelType;
        }
        return ct;
    }

    private static IContentTypeRepository CreateRepository(
        params (int id, Type? modelType)[] contentTypes)
    {
        var repo = Substitute.For<IContentTypeRepository>();
        var ctList = new List<ContentType>();

        foreach (var (id, modelType) in contentTypes)
        {
            ctList.Add(CreateContentType(id, modelType));
        }

        repo.List().Returns(ctList);
        return repo;
    }

    // --- Tests ---

    [Fact]
    public void GetRestrictions_ReturnsEmptyDictionary_ForUnknownAttributeName()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-nonexistent");

        Assert.Empty(result);
    }

    [Fact]
    public void GetRestrictions_ReturnsEmptyDictionary_WhenNoContentTypes()
    {
        var repo = Substitute.For<IContentTypeRepository>();
        repo.List().Returns(new List<ContentType>());
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-anything");

        Assert.Empty(result);
    }

    [Fact]
    public void GetRestrictions_ReturnsAllowedOptionIds_ForContentAreaItemOptionsAttribute()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Single(result);
        Assert.True(result.ContainsKey(42));
        Assert.Equal(["black", "white"], result[42]);
    }

    [Fact]
    public void GetRestrictions_ReturnsNull_ForHideAttribute()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-margin");

        Assert.Single(result);
        Assert.True(result.ContainsKey(10));
        Assert.Null(result[10]);
    }

    [Fact]
    public void GetRestrictions_HandlesMultipleAttributesOnSameType()
    {
        var repo = CreateRepository((5, typeof(BlockWithMultipleAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var themeResult = resolver.GetRestrictions("data-theme");
        Assert.Single(themeResult);
        Assert.True(themeResult.ContainsKey(5));
        Assert.Empty(themeResult[5]!); // all options enabled (empty array)

        var layoutResult = resolver.GetRestrictions("data-layout");
        Assert.Single(layoutResult);
        Assert.True(layoutResult.ContainsKey(5));
        Assert.Equal(["wide"], layoutResult[5]);
    }

    [Fact]
    public void GetRestrictions_SkipsContentTypes_WithNullModelType()
    {
        var repo = CreateRepository((1, null));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Empty(result);
    }

    [Fact]
    public void GetRestrictions_HandlesMultipleContentTypes()
    {
        var repo = CreateRepository(
            (1, typeof(BlockWithThemeRestriction)),
            (2, typeof(BlockWithMultipleAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Equal(2, result.Count);
        Assert.Equal(["black", "white"], result[1]);
        Assert.Empty(result[2]!); // all options enabled
    }

    [Fact]
    public void GetRestrictions_HideOverridesOptions_OnSameType()
    {
        var repo = CreateRepository((99, typeof(BlockWithConflictingAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Single(result);
        // Hide runs after Options in the code, so null wins
        Assert.Null(result[99]);
    }

    [Fact]
    public void GetRestrictions_MixedAttributes_SeparateSelectors()
    {
        var repo = CreateRepository((7, typeof(BlockWithMixedAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var themeResult = resolver.GetRestrictions("data-theme");
        Assert.Single(themeResult);
        Assert.Equal(["red", "green"], themeResult[7]);

        var marginResult = resolver.GetRestrictions("data-margin");
        Assert.Single(marginResult);
        Assert.Null(marginResult[7]); // hidden
    }

    [Fact]
    public void GetRestrictions_IsCached_OnSubsequentCalls()
    {
        var repo = CreateRepository((1, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        // Call multiple times
        _ = resolver.GetRestrictions("data-theme");
        _ = resolver.GetRestrictions("data-theme");
        _ = resolver.GetRestrictions("data-margin");

        // Repository.List() should only be called once (lazy initialization)
        repo.Received(1).List();
    }

    [Fact]
    public void GetRestrictions_ContentTypeWithNoAttributes_NotIncludedInResult()
    {
        var repo = CreateRepository(
            (1, typeof(BlockWithThemeRestriction)),
            (2, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Single(result);
        Assert.True(result.ContainsKey(1));
        Assert.False(result.ContainsKey(2));
    }

    // --- IsOptionApplicable tests ---

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenNoRestrictions_AvailabilityAll()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
            Availability = ContentAreaItemOptionsAvailability.All,
        };

        Assert.True(resolver.IsOptionApplicable(selector, "black", 1));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenNoRestrictions_AvailabilitySpecific()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
            Availability = ContentAreaItemOptionsAvailability.Specific,
        };

        Assert.False(resolver.IsOptionApplicable(selector, "black", 1));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenOptionIsInAllowedList()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        Assert.True(resolver.IsOptionApplicable(selector, "black", 42));
        Assert.True(resolver.IsOptionApplicable(selector, "white", 42));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenOptionIsNotInAllowedList()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        Assert.False(resolver.IsOptionApplicable(selector, "blue", 42));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenSelectorIsHidden()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };

        Assert.False(resolver.IsOptionApplicable(selector, "top", 10));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenEmptyAllowedList_MeansAllOptions()
    {
        // BlockWithMultipleAttributes has [ContentAreaItemOptions("data-theme")] → empty allowed list
        var repo = CreateRepository((5, typeof(BlockWithMultipleAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        Assert.True(resolver.IsOptionApplicable(selector, "any-option", 5));
    }

    [Fact]
    public void IsOptionApplicable_IsCaseInsensitive_ForOptionId()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        Assert.True(resolver.IsOptionApplicable(selector, "BLACK", 42));
        Assert.True(resolver.IsOptionApplicable(selector, "White", 42));
    }

    // --- GetApplicableCssClasses tests ---

    [Fact]
    public void GetApplicableCssClasses_ReturnsClasses_ForApplicableOptions()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        selector.Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" });
        selector.Add(new ContentAreaItemOption { Id = "white", Name = "White", CssClass = "theme-white" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object> { ["data-theme"] = "black" };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1);

        Assert.Equal("theme-black", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsHiddenSelector()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };
        selector.Add(new ContentAreaItemOption { Id = "top", Name = "Top", CssClass = "margin-top" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object> { ["data-margin"] = "top" };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 10);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsRestrictedOption()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        selector.Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" });
        selector.Add(new ContentAreaItemOption { Id = "blue", Name = "Blue", CssClass = "theme-blue" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        // "blue" is not in the allowed list for content type 42 (only "black" and "white")
        var renderSettings = new Dictionary<string, object> { ["data-theme"] = "blue" };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 42);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_ReturnsMultipleClasses_FromMultipleSelectors()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var themeSelector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        themeSelector.Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" });

        var marginSelector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };
        marginSelector.Add(new ContentAreaItemOption { Id = "top", Name = "Top", CssClass = "margin-top" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(themeSelector);
        registry.Add(marginSelector);

        var renderSettings = new Dictionary<string, object>
        {
            ["data-theme"] = "black",
            ["data-margin"] = "top",
        };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1);

        Assert.Equal("theme-black margin-top", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsRestrictionCheck_WhenContentTypeIdIsNull()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };
        selector.Add(new ContentAreaItemOption { Id = "top", Name = "Top", CssClass = "margin-top" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object> { ["data-margin"] = "top" };

        // null contentTypeId → no restriction check, returns the class
        var result = resolver.GetApplicableCssClasses(registry, renderSettings, null);

        Assert.Equal("margin-top", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsOption_WithNullCssClass()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        selector.Add(new ContentAreaItemOption { Id = "none", Name = "None", CssClass = null });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object> { ["data-theme"] = "none" };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsSelector_WhenNotInRenderSettings()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        selector.Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object>();

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsSpecificSelector_WhenContentTypeNotOptedIn()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-layout",
            SelectorName = "layout",
            LabelPrefix = "Layout",
            Availability = ContentAreaItemOptionsAvailability.Specific,
        };
        selector.Add(new ContentAreaItemOption { Id = "wide", Name = "Wide", CssClass = "layout-wide" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        // Stale render setting from when the option was previously enabled
        var renderSettings = new Dictionary<string, object> { ["data-layout"] = "wide" };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1);

        Assert.Equal("", result);
    }
}
