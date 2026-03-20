using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionsAvailabilityTests
{
    [Fact]
    public void All_HasExpectedValue()
    {
        Assert.Equal(0, (int)ContentAreaItemOptionsAvailability.All);
    }

    [Fact]
    public void Specific_HasExpectedValue()
    {
        Assert.Equal(1, (int)ContentAreaItemOptionsAvailability.Specific);
    }

    [Fact]
    public void None_HasExpectedValue()
    {
        Assert.Equal(2, (int)ContentAreaItemOptionsAvailability.None);
    }

    [Theory]
    [InlineData("All", ContentAreaItemOptionsAvailability.All)]
    [InlineData("Specific", ContentAreaItemOptionsAvailability.Specific)]
    [InlineData("None", ContentAreaItemOptionsAvailability.None)]
    public void ToString_ReturnsCorrectName(string expected, ContentAreaItemOptionsAvailability value)
    {
        Assert.Equal(expected, value.ToString());
    }

    [Fact]
    public void EnumValues_AreDistinct()
    {
        var values = Enum.GetValues<ContentAreaItemOptionsAvailability>();

        Assert.Equal(3, values.Length);
        Assert.Equal(values.Distinct().Count(), values.Length);
    }
}
