using EPiServer.DataAbstraction;
using NSubstitute;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

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
}
