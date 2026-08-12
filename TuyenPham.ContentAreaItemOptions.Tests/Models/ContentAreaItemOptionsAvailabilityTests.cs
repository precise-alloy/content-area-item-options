using TuyenPham.ContentAreaItemOptions.Models;
using ItemOptions = TuyenPham.ContentAreaItemOptions.Models.ContentAreaItemOptions;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionsAvailabilityTests
{
    [Theory]
    [InlineData(ContentAreaItemOptionsAvailability.All, "All")]
    [InlineData(ContentAreaItemOptionsAvailability.Specific, "Specific")]
    [InlineData(ContentAreaItemOptionsAvailability.None, "None")]
    public void ToString_ReturnsNameSentToTheClient(ContentAreaItemOptionsAvailability value, string expected)
    {
        // The Dojo command compares against these exact strings.
        Assert.Equal(expected, value.ToString());
    }

    [Fact]
    public void All_IsTheDefault()
    {
        var selector = new ItemOptions
        {
            AttributeName = "data-theme",
            SelectorName = "theme",
            LabelPrefix = "Theme",
        };

        Assert.Equal(ContentAreaItemOptionsAvailability.All, selector.Availability);
    }

    [Fact]
    public void EnumValues_AreDistinct()
    {
        var values = Enum.GetValues<ContentAreaItemOptionsAvailability>();

        Assert.Equal(3, values.Length);
        Assert.Equal(values.Distinct().Count(), values.Length);
    }
}
