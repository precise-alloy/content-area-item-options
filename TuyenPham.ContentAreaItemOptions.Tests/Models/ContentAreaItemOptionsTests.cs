using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionsTests
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
    public void DefaultLabel_DefaultsToDefault()
    {
        var selector = CreateSelector();
        Assert.Equal("Default", selector.DefaultLabel);
    }

    [Fact]
    public void DefaultLabel_CanBeOverridden()
    {
        var selector = new ContentAreaItemOptions.Models.ContentAreaItemOptions
        {
            AttributeName = "data-test",
            SelectorName = "test",
            LabelPrefix = "Test",
            DefaultLabel = "None Selected"
        };

        Assert.Equal("None Selected", selector.DefaultLabel);
    }

    [Fact]
    public void Availability_DefaultsToAll()
    {
        var selector = CreateSelector();
        Assert.Equal(ContentAreaItemOptionsAvailability.All, selector.Availability);
    }

    [Fact]
    public void Availability_CanBeSetToSpecific()
    {
        var selector = new ContentAreaItemOptions.Models.ContentAreaItemOptions
        {
            AttributeName = "data-test",
            SelectorName = "test",
            LabelPrefix = "Test",
            Availability = ContentAreaItemOptionsAvailability.Specific
        };

        Assert.Equal(ContentAreaItemOptionsAvailability.Specific, selector.Availability);
    }

    [Fact]
    public void Availability_CanBeSetToNone()
    {
        var selector = new ContentAreaItemOptions.Models.ContentAreaItemOptions
        {
            AttributeName = "data-test",
            SelectorName = "test",
            LabelPrefix = "Test",
            Availability = ContentAreaItemOptionsAvailability.None
        };

        Assert.Equal(ContentAreaItemOptionsAvailability.None, selector.Availability);
    }

    [Fact]
    public void Add_ReturnsSelf_ForFluentChaining()
    {
        var selector = CreateSelector();
        var result = selector.Add(new ContentAreaItemOption { Id = "a" });

        Assert.Same(selector, result);
    }

    [Fact]
    public void Add_AddsOptionToCollection()
    {
        var selector = CreateSelector();
        var option = new ContentAreaItemOption { Id = "a", Name = "A" };

        selector.Add(option);

        Assert.Single(selector);
        Assert.Same(option, selector.First());
    }

    [Fact]
    public void Add_MultipleItems_PreservesOrder()
    {
        var selector = CreateSelector();
        var opt1 = new ContentAreaItemOption { Id = "a" };
        var opt2 = new ContentAreaItemOption { Id = "b" };
        var opt3 = new ContentAreaItemOption { Id = "c" };

        selector.Add(opt1).Add(opt2).Add(opt3);

        var list = selector.ToList();
        Assert.Equal(3, list.Count);
        Assert.Same(opt1, list[0]);
        Assert.Same(opt2, list[1]);
        Assert.Same(opt3, list[2]);
    }

    [Fact]
    public void Get_FindsOptionById_ExactMatch()
    {
        var selector = CreateSelector();
        var option = new ContentAreaItemOption { Id = "MyOption" };
        selector.Add(option);

        Assert.Same(option, selector.Get("MyOption"));
    }

    [Theory]
    [InlineData("myoption")]
    [InlineData("MYOPTION")]
    [InlineData("MyOption")]
    [InlineData("myOption")]
    public void Get_FindsOptionById_CaseInsensitive(string lookupId)
    {
        var selector = CreateSelector();
        var option = new ContentAreaItemOption { Id = "MyOption" };
        selector.Add(option);

        Assert.Same(option, selector.Get(lookupId));
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotFound()
    {
        var selector = CreateSelector();
        selector.Add(new ContentAreaItemOption { Id = "a" });

        Assert.Null(selector.Get("nonexistent"));
    }

    [Fact]
    public void Get_ReturnsFirstMatch_WhenMultipleOptionsExist()
    {
        var selector = CreateSelector();
        var first = new ContentAreaItemOption { Id = "dup", Name = "First" };
        var second = new ContentAreaItemOption { Id = "dup", Name = "Second" };
        selector.Add(first).Add(second);

        Assert.Same(first, selector.Get("dup"));
    }

    [Fact]
    public void Enumeration_WorksViaForeach()
    {
        var selector = CreateSelector();
        selector.Add(new ContentAreaItemOption { Id = "a" });
        selector.Add(new ContentAreaItemOption { Id = "b" });

        var ids = new List<string>();
        foreach (var opt in selector)
        {
            ids.Add(opt.Id!);
        }

        Assert.Equal(["a", "b"], ids);
    }

    [Fact]
    public void Enumeration_EmptySelector_ReturnsNoItems()
    {
        var selector = CreateSelector();

        Assert.Empty(selector);
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var selector = CreateSelector();
        selector.Add(new ContentAreaItemOption { Id = "a" });

        var enumerable = (System.Collections.IEnumerable)selector;
        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void RequiredProperties_AreSet()
    {
        var selector = new ContentAreaItemOptions.Models.ContentAreaItemOptions
        {
            AttributeName = "data-custom-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme"
        };

        Assert.Equal("data-custom-theme", selector.AttributeName);
        Assert.Equal("theme", selector.SelectorName);
        Assert.Equal("Theme", selector.LabelPrefix);
    }
}
