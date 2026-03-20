using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionsAttributeTests
{
    [Fact]
    public void Constructor_SetsAttributeName()
    {
        var attr = new ContentAreaItemOptionsAttribute("data-theme");

        Assert.Equal("data-theme", attr.AttributeName);
    }

    [Fact]
    public void Constructor_WithNoOptionIds_SetsEmptyArray()
    {
        var attr = new ContentAreaItemOptionsAttribute("data-theme");

        Assert.NotNull(attr.AllowedOptionIds);
        Assert.Empty(attr.AllowedOptionIds);
    }

    [Fact]
    public void Constructor_WithOptionIds_SetsAllowedOptionIds()
    {
        var attr = new ContentAreaItemOptionsAttribute("data-theme", "black", "white");

        Assert.Equal(["black", "white"], attr.AllowedOptionIds);
    }

    [Fact]
    public void Constructor_WithSingleOptionId_SetsArray()
    {
        var attr = new ContentAreaItemOptionsAttribute("data-theme", "black");

        Assert.Equal(["black"], attr.AllowedOptionIds);
    }

    [Fact]
    public void Attribute_CanBeAppliedToClass()
    {
        var attrs = typeof(ClassWithAttribute)
            .GetCustomAttributes(typeof(ContentAreaItemOptionsAttribute), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void Attribute_CanBeAppliedToProperty()
    {
        var prop = typeof(PropertyWithAttribute)
            .GetProperty(nameof(PropertyWithAttribute.ContentArea))!;
        var attrs = prop.GetCustomAttributes(typeof(ContentAreaItemOptionsAttribute), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void Attribute_AllowsMultipleOnClass()
    {
        var attrs = typeof(MultiAttributeBlock)
            .GetCustomAttributes(typeof(ContentAreaItemOptionsAttribute), false);

        Assert.Equal(2, attrs.Length);
    }

    [Fact]
    public void Attribute_AllowsMultipleOnProperty()
    {
        var prop = typeof(MultiAttributeProperty)
            .GetProperty(nameof(MultiAttributeProperty.ContentArea))!;
        var attrs = prop.GetCustomAttributes(typeof(ContentAreaItemOptionsAttribute), false);

        Assert.Equal(2, attrs.Length);
    }

    [Fact]
    public void Attribute_PreservesAllowedOptionIds_Values()
    {
        var attrs = typeof(MultiAttributeBlock)
            .GetCustomAttributes(typeof(ContentAreaItemOptionsAttribute), false)
            .Cast<ContentAreaItemOptionsAttribute>()
            .OrderBy(a => a.AttributeName)
            .ToList();

        Assert.Equal("data-margin", attrs[0].AttributeName);
        Assert.Equal(["top", "bottom"], attrs[0].AllowedOptionIds);

        Assert.Equal("data-theme", attrs[1].AttributeName);
        Assert.Equal(["black"], attrs[1].AllowedOptionIds);
    }

    [ContentAreaItemOptions("data-theme")]
    private class ClassWithAttribute { }

    private class PropertyWithAttribute
    {
        [ContentAreaItemOptions("data-theme")]
        public object? ContentArea { get; set; }
    }

    [ContentAreaItemOptions("data-theme", "black")]
    [ContentAreaItemOptions("data-margin", "top", "bottom")]
    private class MultiAttributeBlock { }

    private class MultiAttributeProperty
    {
        [ContentAreaItemOptions("data-theme")]
        [ContentAreaItemOptions("data-margin", "top")]
        public object? ContentArea { get; set; }
    }
}
