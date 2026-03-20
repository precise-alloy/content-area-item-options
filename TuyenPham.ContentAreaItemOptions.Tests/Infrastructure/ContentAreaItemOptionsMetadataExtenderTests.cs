using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Infrastructure;

public class ContentAreaItemOptionsMetadataExtenderTests
{
    [Fact]
    public void BuildOverrides_ReturnsNull_WhenEmptyAttributes()
    {
        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides([]);

        Assert.Null(result);
    }

    [Fact]
    public void BuildOverrides_ReturnsNull_WhenNoRelevantAttributes()
    {
        var attributes = new Attribute[] { new ObsoleteAttribute() };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.Null(result);
    }

    [Fact]
    public void BuildOverrides_MapsContentAreaItemOptionsAttribute_WithSpecificIds()
    {
        var attributes = new Attribute[]
        {
            new ContentAreaItemOptionsAttribute("data-theme", "black", "white"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("data-theme"));
        Assert.Equal(["black", "white"], result["data-theme"]);
    }

    [Fact]
    public void BuildOverrides_MapsContentAreaItemOptionsAttribute_WithNoIds()
    {
        var attributes = new Attribute[]
        {
            new ContentAreaItemOptionsAttribute("data-theme"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("data-theme"));
        Assert.NotNull(result["data-theme"]);
        Assert.Empty(result["data-theme"]!);
    }

    [Fact]
    public void BuildOverrides_MapsHideAttribute_ToNull()
    {
        var attributes = new Attribute[]
        {
            new HideContentAreaItemOptionsAttribute("data-margin"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("data-margin"));
        Assert.Null(result["data-margin"]);
    }

    [Fact]
    public void BuildOverrides_HideOverridesOptions_ForSameAttributeName()
    {
        var attributes = new Attribute[]
        {
            new ContentAreaItemOptionsAttribute("data-theme", "black", "white"),
            new HideContentAreaItemOptionsAttribute("data-theme"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Null(result["data-theme"]);
    }

    [Fact]
    public void BuildOverrides_HandlesMultipleSelectors()
    {
        var attributes = new Attribute[]
        {
            new ContentAreaItemOptionsAttribute("data-theme", "black"),
            new ContentAreaItemOptionsAttribute("data-margin", "top", "bottom"),
            new HideContentAreaItemOptionsAttribute("data-layout"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(["black"], result["data-theme"]);
        Assert.Equal(["top", "bottom"], result["data-margin"]);
        Assert.Null(result["data-layout"]);
    }

    [Fact]
    public void BuildOverrides_OnlyHideAttributes_ReturnsNonNull()
    {
        var attributes = new Attribute[]
        {
            new HideContentAreaItemOptionsAttribute("data-theme"),
            new HideContentAreaItemOptionsAttribute("data-margin"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Null(result["data-theme"]);
        Assert.Null(result["data-margin"]);
    }

    [Fact]
    public void BuildOverrides_MixedAttributes_HideDoesNotAffectOtherSelectors()
    {
        var attributes = new Attribute[]
        {
            new ContentAreaItemOptionsAttribute("data-theme", "dark", "light"),
            new HideContentAreaItemOptionsAttribute("data-margin"),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(["dark", "light"], result["data-theme"]);
        Assert.Null(result["data-margin"]);
    }

    [Fact]
    public void BuildOverrides_IgnoresNonRelevantAttributes_InMixedList()
    {
        var attributes = new Attribute[]
        {
            new ObsoleteAttribute(),
            new ContentAreaItemOptionsAttribute("data-theme", "black"),
            new SerializableAttribute(),
        };

        var result = ContentAreaItemOptionsMetadataExtender.BuildOverrides(attributes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(["black"], result["data-theme"]);
    }
}
