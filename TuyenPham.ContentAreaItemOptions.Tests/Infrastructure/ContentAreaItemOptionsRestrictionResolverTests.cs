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

    [ContentAreaItemOptions("data-retired")]
    private class BlockOptingIntoRetiredSelector { }

    // --- Helpers ---

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

    private static ItemOptions CreateSelector(
        string attributeName,
        ContentAreaItemOptionsAvailability availability = ContentAreaItemOptionsAvailability.All,
        params (string id, string? cssClass)[] options)
    {
        var selector = new ItemOptions
        {
            AttributeName = attributeName,
            SelectorName = attributeName.Replace("data-", ""),
            LabelPrefix = attributeName,
            Availability = availability,
        };

        foreach (var (id, cssClass) in options)
        {
            selector.Add(new ContentAreaItemOption { Id = id, Name = id, CssClass = cssClass });
        }

        return selector;
    }

    // --- GetRestrictions ---

    [Fact]
    public void GetRestrictions_ReturnsEmptyDictionary_ForUnknownAttributeName()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Empty(resolver.GetRestrictions("data-nonexistent"));
    }

    [Fact]
    public void GetRestrictions_ReturnsEmptyDictionary_WhenNoContentTypes()
    {
        var repo = Substitute.For<IContentTypeRepository>();
        repo.List().Returns([]);
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Empty(resolver.GetRestrictions("data-anything"));
    }

    [Fact]
    public void GetRestrictions_ReturnsAllowedOptionIds_ForContentAreaItemOptionsAttribute()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-theme");

        Assert.Single(result);
        Assert.Equal(["black", "white"], result[42]!);
    }

    [Fact]
    public void GetRestrictions_ReturnsNull_ForHideAttribute()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var result = resolver.GetRestrictions("data-margin");

        Assert.Single(result);
        Assert.Null(result[10]);
    }

    [Fact]
    public void GetRestrictions_IsCaseInsensitive_ForAttributeName()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Single(resolver.GetRestrictions("DATA-THEME"));
    }

    [Fact]
    public void GetRestrictions_HandlesMultipleAttributesOnSameType()
    {
        var repo = CreateRepository((5, typeof(BlockWithMultipleAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Empty(resolver.GetRestrictions("data-theme")[5]!); // no ids = all enabled
        Assert.Equal(["wide"], resolver.GetRestrictions("data-layout")[5]!);
    }

    [Fact]
    public void GetRestrictions_SkipsContentTypes_WithNullModelType()
    {
        var repo = CreateRepository((1, null));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Empty(resolver.GetRestrictions("data-theme"));
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
        Assert.Equal(["black", "white"], result[1]!);
        Assert.Empty(result[2]!);
    }

    [Fact]
    public void GetRestrictions_HideOverridesOptions_OnSameType()
    {
        var repo = CreateRepository((99, typeof(BlockWithConflictingAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Null(resolver.GetRestrictions("data-theme")[99]);
    }

    [Fact]
    public void GetRestrictions_MixedAttributes_SeparateSelectors()
    {
        var repo = CreateRepository((7, typeof(BlockWithMixedAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Equal(["red", "green"], resolver.GetRestrictions("data-theme")[7]!);
        Assert.Null(resolver.GetRestrictions("data-margin")[7]);
    }

    [Fact]
    public void GetRestrictions_IsCached_OnSubsequentCalls()
    {
        var repo = CreateRepository((1, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        _ = resolver.GetRestrictions("data-theme");
        _ = resolver.GetRestrictions("data-theme");
        _ = resolver.GetRestrictions("data-margin");

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
        Assert.False(result.ContainsKey(2));
    }

    // --- IsOptionApplicable: availability ---

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenNoRestrictions_AvailabilityAll()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 1));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenNoRestrictions_AvailabilitySpecific()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-theme", ContentAreaItemOptionsAvailability.Specific);

        Assert.False(resolver.IsOptionApplicable(selector, "black", 1));
    }

    [Fact]
    public void IsOptionApplicable_ThrowsArgumentNullException_WhenSelectorIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Throws<ArgumentNullException>(() => resolver.IsOptionApplicable(null!, "black", 1));
    }

    // --- IsOptionApplicable: Availability.None ---

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenAvailabilityIsNone()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None);

        Assert.False(resolver.IsOptionApplicable(selector, "anything", 1));
    }

    [Fact]
    public void IsOptionApplicable_AvailabilityNone_IgnoresContentTypeOptIn()
    {
        var repo = CreateRepository((3, typeof(BlockOptingIntoRetiredSelector)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None);

        // The block has [ContentAreaItemOptions("data-retired")], which would normally enable everything.
        Assert.False(resolver.IsOptionApplicable(selector, "anything", 3));
    }

    [Fact]
    public void IsOptionApplicable_AvailabilityNone_IgnoresPropertyOverride()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None);

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-retired"] = [] };

        Assert.False(resolver.IsOptionApplicable(selector, "anything", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_AvailabilityNone_AppliesWhenContentTypeIdIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None);

        Assert.False(resolver.IsOptionApplicable(selector, "anything", null));
    }

    // --- IsOptionApplicable: content-type restrictions ---

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenOptionIsInAllowedList()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-theme");

        Assert.True(resolver.IsOptionApplicable(selector, "black", 42));
        Assert.True(resolver.IsOptionApplicable(selector, "white", 42));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenOptionIsNotInAllowedList()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-theme"), "blue", 42));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenSelectorIsHidden()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-margin"), "top", 10));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenEmptyAllowedList_MeansAllOptions()
    {
        var repo = CreateRepository((5, typeof(BlockWithMultipleAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-theme"), "any-option", 5));
    }

    [Fact]
    public void IsOptionApplicable_IsCaseInsensitive_ForOptionId()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-theme");

        Assert.True(resolver.IsOptionApplicable(selector, "BLACK", 42));
        Assert.True(resolver.IsOptionApplicable(selector, "White", 42));
    }

    [Fact]
    public void IsOptionApplicable_UnknownContentTypeId_FallsBackToAvailability()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-theme"), "blue", 999));
    }

    // --- IsOptionApplicable: unresolved content type ---

    [Fact]
    public void IsOptionApplicable_NullContentTypeId_SkipsContentTypeRestrictions()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        // The hide is content-type scoped and cannot be evaluated without an id.
        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-margin"), "top", null));
    }

    [Fact]
    public void IsOptionApplicable_NullContentTypeId_StillEnforcesAvailabilitySpecific()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);

        Assert.False(resolver.IsOptionApplicable(selector, "wide", null));
    }

    [Fact]
    public void IsOptionApplicable_NullContentTypeId_StillEnforcesPropertyOverrides()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", null, propertyOverrides));
    }

    // --- IsOptionApplicable: property overrides ---

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenPropertyOverrideEnablesSpecificSelector()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = ["wide", "narrow"] };

        Assert.True(resolver.IsOptionApplicable(selector, "wide", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenPropertyOverrideRestrictsOption()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = ["wide"] };

        Assert.False(resolver.IsOptionApplicable(selector, "narrow", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsFalse_WhenPropertyOverrideHidesSelector()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_MatchesPropertyOverrideAttributeName_CaseInsensitively()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = ContentAreaItemOptionsMetadataExtender.BuildOverrides(
        [
            new HideContentAreaItemOptionsAttribute("DATA-THEME"),
        ]);

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_ReturnsTrue_WhenPropertyOverrideHasEmptyAllowedList()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = [] };

        Assert.True(resolver.IsOptionApplicable(selector, "any-option", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_IgnoresPropertyOverrides_ForOtherSelectors()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = [] };

        Assert.False(resolver.IsOptionApplicable(selector, "wide", 1, propertyOverrides));
    }

    [Fact]
    public void IsOptionApplicable_IgnoresPropertyOverrides_WhenNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);

        Assert.False(resolver.IsOptionApplicable(selector, "wide", 1, null));
    }

    // --- Precedence: content type > ContentArea property > availability ---

    [Fact]
    public void Precedence_ContentTypeAllows_OverridesPropertyHide()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 42, propertyOverrides));
    }

    [Fact]
    public void Precedence_ContentTypeHides_OverridesPropertyAllow()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-margin"] = [] };

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-margin"), "top", 10, propertyOverrides));
    }

    [Fact]
    public void Precedence_ContentTypeRestricts_OverridesPropertyBroaderAllow()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-theme");
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = [] };

        Assert.True(resolver.IsOptionApplicable(selector, "black", 42, propertyOverrides));
        Assert.False(resolver.IsOptionApplicable(selector, "blue", 42, propertyOverrides));
    }

    [Fact]
    public void Precedence_PropertyOverride_OverridesGlobalSpecificAvailability()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var selector = CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = ["wide"] };

        Assert.True(resolver.IsOptionApplicable(selector, "wide", 1, propertyOverrides));
        Assert.False(resolver.IsOptionApplicable(selector, "narrow", 1, propertyOverrides));
    }

    [Fact]
    public void Precedence_PropertyHide_OverridesGlobalAllAvailability()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        Assert.False(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 1, propertyOverrides));
    }

    [Fact]
    public void Precedence_NoRestrictionNoOverride_FallsBackToGlobalAvailability()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.True(resolver.IsOptionApplicable(CreateSelector("data-theme"), "black", 1));
        Assert.False(resolver.IsOptionApplicable(
            CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific), "wide", 1));
    }

    // --- GetApplicableCssClasses ---

    [Fact]
    public void GetApplicableCssClasses_ReturnsClasses_ForApplicableOptions()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black"), ("white", "theme-white")]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "black" }, 1);

        Assert.Equal("theme-black", result);
    }

    [Fact]
    public void GetApplicableCssClasses_MatchesAttributeName_CaseInsensitively()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        // ContentAreaItem.RenderSettings is an OrdinalIgnoreCase dictionary.
        var renderSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DATA-THEME"] = "black",
        };

        Assert.Equal("theme-black", resolver.GetApplicableCssClasses(registry, renderSettings, 1));
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsHiddenSelector()
    {
        var repo = CreateRepository((10, typeof(BlockWithHiddenMargin)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-margin", options: [("top", "margin-top")]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-margin"] = "top" }, 10);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsRestrictedOption()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black"), ("blue", "theme-blue")]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "blue" }, 42);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_ReturnsMultipleClasses_InRegistryOrder()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
            CreateSelector("data-margin", options: [("top", "margin-top")]),
        };

        var renderSettings = new Dictionary<string, string>
        {
            ["data-margin"] = "top",
            ["data-theme"] = "black",
        };

        Assert.Equal("theme-black margin-top", resolver.GetApplicableCssClasses(registry, renderSettings, 1));
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsHiddenSelector_WhenContentTypeIdIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        // Inline blocks have no content link, so the content type cannot be resolved.
        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "black" }, null, propertyOverrides);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsSpecificSelector_WhenContentTypeIdIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific, ("wide", "layout-wide")),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-layout"] = "wide" }, null);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsRetiredSelector()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None, ("old", "retired-old")),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-retired"] = "old" }, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsOption_WithNullCssClass()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("none", null)]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "none" }, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsOption_WithWhitespaceCssClass()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "   ")]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "black" }, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsUnknownOptionId()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "removed-option" }, 1);

        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetApplicableCssClasses_SkipsSelector_WhenStoredValueIsEmpty(string? storedValue)
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        // The editor writes null when the "Default" entry is selected.
        var renderSettings = new Dictionary<string, string> { ["data-theme"] = storedValue! };

        Assert.Equal("", resolver.GetApplicableCssClasses(registry, renderSettings, 1));
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsSelector_WhenNotInRenderSettings()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        Assert.Equal("", resolver.GetApplicableCssClasses(registry, new Dictionary<string, string>(), 1));
    }

    [Fact]
    public void GetApplicableCssClasses_ReturnsEmpty_WhenRenderSettingsIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        Assert.Equal("", resolver.GetApplicableCssClasses(registry, null, 1));
    }

    [Fact]
    public void GetApplicableCssClasses_ThrowsArgumentNullException_WhenRegistryIsNull()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        Assert.Throws<ArgumentNullException>(() =>
            resolver.GetApplicableCssClasses(null!, new Dictionary<string, string>(), 1));
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsSpecificSelector_WhenContentTypeNotOptedIn()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific, ("wide", "layout-wide")),
        };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-layout"] = "wide" }, 1);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_IncludesOption_WhenPropertyOverrideEnablesSpecificSelector()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector(
                "data-layout",
                ContentAreaItemOptionsAvailability.Specific,
                ("1-12", "col-1-12"),
                ("3-12", "col-3-12")),
        };

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = ["1-12", "3-12"] };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-layout"] = "1-12" }, 1, propertyOverrides);

        Assert.Equal("col-1-12", result);
    }

    [Fact]
    public void GetApplicableCssClasses_SkipsOption_WhenPropertyOverrideHidesSelector()
    {
        var repo = CreateRepository((1, typeof(BlockWithNoAttributes)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black")]),
        };

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = null };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "black" }, 1, propertyOverrides);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_ContentTypeRestriction_TakesPriority_OverPropertyOverride()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("blue", "theme-blue")]),
        };

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-theme"] = ["blue"] };

        var result = resolver.GetApplicableCssClasses(
            registry, new Dictionary<string, string> { ["data-theme"] = "blue" }, 42, propertyOverrides);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetApplicableCssClasses_FullPrecedenceChain()
    {
        var repo = CreateRepository((42, typeof(BlockWithThemeRestriction)));
        var resolver = new ContentAreaItemOptionsRestrictionResolver(repo);

        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme", options: [("black", "theme-black"), ("blue", "theme-blue")]),
            CreateSelector("data-layout", ContentAreaItemOptionsAvailability.Specific, ("wide", "layout-wide")),
            CreateSelector("data-margin", options: [("top", "margin-top")]),
            CreateSelector("data-retired", ContentAreaItemOptionsAvailability.None, ("old", "retired-old")),
        };

        var renderSettings = new Dictionary<string, string>
        {
            ["data-theme"] = "blue",    // blocked by the block-type restriction
            ["data-layout"] = "wide",   // enabled by the property override
            ["data-margin"] = "top",    // allowed by Availability.All
            ["data-retired"] = "old",   // never applied
        };

        var propertyOverrides = new Dictionary<string, string[]?> { ["data-layout"] = ["wide"] };

        var result = resolver.GetApplicableCssClasses(registry, renderSettings, 42, propertyOverrides);

        Assert.Equal("layout-wide margin-top", result);
    }
}
