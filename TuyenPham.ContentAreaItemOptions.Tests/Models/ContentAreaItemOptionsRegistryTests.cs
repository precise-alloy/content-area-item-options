using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionsRegistryTests
{
    private static ContentAreaItemOptions.Models.ContentAreaItemOptions CreateSelector(
        string attributeName = "data-test",
        string selectorName = "test",
        string labelPrefix = "Test")
    {
        return new ContentAreaItemOptions.Models.ContentAreaItemOptions
        {
            AttributeName = attributeName,
            SelectorName = selectorName,
            LabelPrefix = labelPrefix,
        };
    }

    [Fact]
    public void Add_ReturnsSelf_ForFluentChaining()
    {
        var registry = new ContentAreaItemOptionsRegistry();
        var result = registry.Add(CreateSelector());

        Assert.Same(registry, result);
    }

    [Fact]
    public void Add_AddsSelector()
    {
        var registry = new ContentAreaItemOptionsRegistry();
        var selector = CreateSelector();

        registry.Add(selector);

        Assert.Single(registry);
        Assert.Same(selector, registry.First());
    }

    [Fact]
    public void CollectionInitializer_Works()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-a", "a", "A"),
            CreateSelector("data-b", "b", "B"),
        };

        Assert.Equal(2, registry.Count());
    }

    [Fact]
    public void CollectionInitializer_PreservesOrder()
    {
        var s1 = CreateSelector("data-a", "a", "A");
        var s2 = CreateSelector("data-b", "b", "B");
        var s3 = CreateSelector("data-c", "c", "C");

        var registry = new ContentAreaItemOptionsRegistry { s1, s2, s3 };

        var list = registry.ToList();
        Assert.Same(s1, list[0]);
        Assert.Same(s2, list[1]);
        Assert.Same(s3, list[2]);
    }

    [Theory]
    [InlineData("data-custom-theme")]
    [InlineData("DATA-CUSTOM-THEME")]
    [InlineData("Data-Custom-Theme")]
    public void GetByAttributeName_FindsSelector_CaseInsensitive(string lookup)
    {
        var selector = CreateSelector("data-custom-theme");
        var registry = new ContentAreaItemOptionsRegistry { selector };

        Assert.Same(selector, registry.GetByAttributeName(lookup));
    }

    [Fact]
    public void GetByAttributeName_ReturnsNull_WhenNotFound()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-theme"),
        };

        Assert.Null(registry.GetByAttributeName("data-nonexistent"));
    }

    [Fact]
    public void GetByAttributeName_ReturnsFirstMatch()
    {
        var first = CreateSelector("data-theme", "first", "First");
        var second = CreateSelector("data-theme", "second", "Second");
        var registry = new ContentAreaItemOptionsRegistry { first, second };

        Assert.Same(first, registry.GetByAttributeName("data-theme"));
    }

    [Theory]
    [InlineData("mySelector")]
    [InlineData("MYSELECTOR")]
    [InlineData("myselector")]
    public void GetBySelectorName_FindsSelector_CaseInsensitive(string lookup)
    {
        var selector = CreateSelector(selectorName: "mySelector");
        var registry = new ContentAreaItemOptionsRegistry { selector };

        Assert.Same(selector, registry.GetBySelectorName(lookup));
    }

    [Fact]
    public void GetBySelectorName_ReturnsNull_WhenNotFound()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector(selectorName: "theme"),
        };

        Assert.Null(registry.GetBySelectorName("nonexistent"));
    }

    [Fact]
    public void GetBySelectorName_ReturnsFirstMatch()
    {
        var first = CreateSelector("data-a", "dup", "First");
        var second = CreateSelector("data-b", "dup", "Second");
        var registry = new ContentAreaItemOptionsRegistry { first, second };

        Assert.Same(first, registry.GetBySelectorName("dup"));
    }

    [Fact]
    public void Enumeration_WorksViaForeach()
    {
        var s1 = CreateSelector("data-a", "a", "A");
        var s2 = CreateSelector("data-b", "b", "B");
        var registry = new ContentAreaItemOptionsRegistry { s1, s2 };

        var names = new List<string>();
        foreach (var s in registry)
        {
            names.Add(s.SelectorName);
        }

        Assert.Equal(["a", "b"], names);
    }

    [Fact]
    public void EmptyRegistry_HasNoSelectors()
    {
        var registry = new ContentAreaItemOptionsRegistry();

        Assert.Empty(registry);
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            CreateSelector("data-a", "a", "A"),
        };

        var enumerable = (System.Collections.IEnumerable)registry;
        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }

        Assert.Equal(1, count);
    }
}
