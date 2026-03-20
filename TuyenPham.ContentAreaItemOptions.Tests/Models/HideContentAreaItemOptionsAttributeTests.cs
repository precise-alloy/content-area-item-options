using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class HideContentAreaItemOptionsAttributeTests
{
    [Fact]
    public void Constructor_SetsAttributeName()
    {
        var attr = new HideContentAreaItemOptionsAttribute("data-theme");

        Assert.Equal("data-theme", attr.AttributeName);
    }

    [Fact]
    public void Attribute_CanBeAppliedToClass()
    {
        var attrs = typeof(ClassWithHide)
            .GetCustomAttributes(typeof(HideContentAreaItemOptionsAttribute), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void Attribute_CanBeAppliedToProperty()
    {
        var prop = typeof(PropertyWithHide)
            .GetProperty(nameof(PropertyWithHide.ContentArea))!;
        var attrs = prop.GetCustomAttributes(typeof(HideContentAreaItemOptionsAttribute), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void Attribute_AllowsMultipleOnClass()
    {
        var attrs = typeof(MultiHideBlock)
            .GetCustomAttributes(typeof(HideContentAreaItemOptionsAttribute), false);

        Assert.Equal(2, attrs.Length);
    }

    [Fact]
    public void Attribute_AllowsMultipleOnProperty()
    {
        var prop = typeof(MultiHideProperty)
            .GetProperty(nameof(MultiHideProperty.ContentArea))!;
        var attrs = prop.GetCustomAttributes(typeof(HideContentAreaItemOptionsAttribute), false);

        Assert.Equal(2, attrs.Length);
    }

    [Fact]
    public void Attribute_PreservesAttributeNames()
    {
        var attrs = typeof(MultiHideBlock)
            .GetCustomAttributes(typeof(HideContentAreaItemOptionsAttribute), false)
            .Cast<HideContentAreaItemOptionsAttribute>()
            .OrderBy(a => a.AttributeName)
            .ToList();

        Assert.Equal("data-margin", attrs[0].AttributeName);
        Assert.Equal("data-theme", attrs[1].AttributeName);
    }

    [HideContentAreaItemOptions("data-theme")]
    private class ClassWithHide { }

    private class PropertyWithHide
    {
        [HideContentAreaItemOptions("data-theme")]
        public object? ContentArea { get; set; }
    }

    [HideContentAreaItemOptions("data-theme")]
    [HideContentAreaItemOptions("data-margin")]
    private class MultiHideBlock { }

    private class MultiHideProperty
    {
        [HideContentAreaItemOptions("data-theme")]
        [HideContentAreaItemOptions("data-margin")]
        public object? ContentArea { get; set; }
    }
}
