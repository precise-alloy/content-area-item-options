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

    // --- IsOptionApplicable with propertyOverrides tests ---

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenPropertyOverrideEnablesSpecificSelector()
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

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = ["wide", "narrow"],
        };

        Assert.True(resolver.IsOptionApplicable(selector, "wide", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenPropertyOverrideRestrictsOption()
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

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = ["wide"],
        };

        Assert.False(resolver.IsOptionApplicable(selector, "narrow", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenPropertyOverrideHidesSelector()
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

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = null, // hidden on this property
        };

        Assert.False(resolver.IsOptionApplicable(selector, "black", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenPropertyOverrideHasEmptyAllowedList()
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

        // Empty array = all options enabled
        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = [],
        };

        Assert.True(resolver.IsOptionApplicable(selector, "any-option", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ContentTypeRestriction_TakesPriority_OverPropertyOverride()
    {
        // Block type hides margin → property override should NOT override that
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-margin"] = ["top", "bottom"],
        };

        Assert.False(resolver.IsOptionApplicable(selector, "top", 10, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_IgnoresPropertyOverrides_WhenNull()
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

        Assert.False(resolver.IsOptionApplicable(selector, "wide", 1, null));
    }

    // --- GetApplicableCssClasses with propertyOverrides tests ---

    [Fact]
    public void GetApplicableCssClasses_IncludesOption_WhenPropertyOverrideEnablesSpecificSelector()
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
        selector.Add(new ContentAreaItemOption { Id = "1-12", Name = "Full", CssClass = "col-1-12" });
        selector.Add(new ContentAreaItemOption { Id = "3-12", Name = "Quarter", CssClass = "col-3-12" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        var renderSettings = new Dictionary<string, object> { ["data-layout"] = "1-12" };
        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = ["1-12", "3-12"],
        };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1, propertyOverrides);

        Assert.Equal("col-1-12", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsOption_WhenPropertyOverrideHidesSelector()
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

        var renderSettings = new Dictionary<string, object> { ["data-theme"] = "black" };
        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = null, // hidden on this property
        };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 1, propertyOverrides);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_ContentTypeRestriction_TakesPriority_OverPropertyOverride()
    {
        // Block type restricts theme to "black" and "white"
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        selector.Add(new ContentAreaItemOption { Id = "blue", Name = "Blue", CssClass = "theme-blue" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(selector);

        // Property allows "blue" but the block type doesn't
        var renderSettings = new Dictionary<string, object> { ["data-theme"] = "blue" };
        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = ["blue"],
        };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 42, propertyOverrides);

        Assert.Equal("", result);
    }

    // --- Precedence: content-type (item) > property (content area) > global (registry) ---

    [Fact]
    public void Precedence_ContentTypeAllows_OverridesPropertyHide()
    {
        // Block type explicitly opts into theme with "black" and "white"
        // Property hides theme — but block-type restriction takes priority
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = null, // property hides it
        };

        // Block type allows "black" → takes priority over property hide
        Assert.True(resolver.IsOptionApplicable(selector, "black", 42, propertyOverrides));
    }

    [Fact]
    public void Precedence_ContentTypeHides_OverridesPropertyAllow()
    {
        // Block type hides margin
        // Property allows margin — but block-type restriction takes priority
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-margin"] = [], // property enables all
        };

        Assert.False(resolver.IsOptionApplicable(selector, "top", 10, propertyOverrides));
    }

    [Fact]
    public void Precedence_ContentTypeRestricts_OverridesPropertyBroaderAllow()
    {
        // Block type only allows "black" and "white" for theme
        // Property allows all theme options — block-type restriction still applies
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = [], // property enables all
        };

        Assert.True(resolver.IsOptionApplicable(selector, "black", 42, propertyOverrides));
        Assert.False(resolver.IsOptionApplicable(selector, "blue", 42, propertyOverrides));
    }

    [Fact]
    public void Precedence_PropertyOverride_OverridesGlobalSpecificAvailability()
    {
        // No block-type restriction, Availability = Specific, property enables the selector
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-layout",
            SelectorName = "layout",
            LabelPrefix = "Layout",
            Availability = ContentAreaItemOptionsAvailability.Specific,
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = ["wide"],
        };

        Assert.True(resolver.IsOptionApplicable(selector, "wide", 1, propertyOverrides));
        Assert.False(resolver.IsOptionApplicable(selector, "narrow", 1, propertyOverrides));
    }

    [Fact]
    public void Precedence_PropertyHide_OverridesGlobalAllAvailability()
    {
        // No block-type restriction, Availability = All, property hides the selector
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
            Availability = ContentAreaItemOptionsAvailability.All,
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-theme"] = null, // property hides it
        };

        Assert.False(resolver.IsOptionApplicable(selector, "black", 1, propertyOverrides));
    }

    [Fact]
    public void Precedence_NoRestrictionNoOverride_FallsBackToGlobalAvailability()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var selectorAll = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
            Availability = ContentAreaItemOptionsAvailability.All,
        };

        var selectorSpecific = new ItemOptions
        {
            AttributeName = "data-layout",
            SelectorName = "layout",
            LabelPrefix = "Layout",
            Availability = ContentAreaItemOptionsAvailability.Specific,
        };

        // Availability.All → allowed
        Assert.True(resolver.IsOptionApplicable(selectorAll, "black", 1));
        // Availability.Specific → denied (no opt-in)
        Assert.False(resolver.IsOptionApplicable(selectorSpecific, "wide", 1));
    }

    [Fact]
    public void Precedence_GetApplicableCssClasses_FullChain()
    {
        // Set up: theme restricted at block level, layout enabled at property level,
        // margin has global Availability.All with no overrides
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var themeSelector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };
        themeSelector.Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" });
        themeSelector.Add(new ContentAreaItemOption { Id = "blue", Name = "Blue", CssClass = "theme-blue" });

        var layoutSelector = new ItemOptions
        {
            AttributeName = "data-layout",
            SelectorName = "layout",
            LabelPrefix = "Layout",
            Availability = ContentAreaItemOptionsAvailability.Specific,
        };
        layoutSelector.Add(new ContentAreaItemOption { Id = "wide", Name = "Wide", CssClass = "layout-wide" });

        var marginSelector = new ItemOptions
        {
            AttributeName = "data-margin",
            SelectorName = "margin",
            LabelPrefix = "Margin",
            Availability = ContentAreaItemOptionsAvailability.All,
        };
        marginSelector.Add(new ContentAreaItemOption { Id = "top", Name = "Top", CssClass = "margin-top" });

        var registry = new ContentAreaItemOptionsRegistry();
        registry.Add(themeSelector);
        registry.Add(layoutSelector);
        registry.Add(marginSelector);

        var renderSettings = new Dictionary<string, object>
        {
            ["data-theme"] = "blue",   // restricted at item level (only black/white)
            ["data-layout"] = "wide",  // enabled at property level
            ["data-margin"] = "top",   // allowed by global Availability.All
        };

        var propertyOverrides = new Dictionary<string, string[]?>
        {
            ["data-layout"] = ["wide"],
        };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 42, propertyOverrides);

        // "blue" is blocked by block-type restriction → no theme class
        // "wide" is enabled by property override → layout-wide included
        // "top" has no restriction, Availability.All → margin-top included
        Assert.Equal("layout-wide margin-top", result);
    }
}
