using TuyenPham.ContentAreaItemOptions.Models;

namespace TuyenPham.ContentAreaItemOptions.Tests.Models;

public class ContentAreaItemOptionTests
{
    [Fact]
    public void Properties_DefaultToNull()
    {
        var option = new ContentAreaItemOption();

        Assert.Null(option.Id);
        Assert.Null(option.Name);
        Assert.Null(option.Description);
        Assert.Null(option.CssClass);
        Assert.Null(option.IconClass);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var option = new ContentAreaItemOption
        {
            Id = "test-id",
            Name = "Test Name",
            Description = "Test Description",
            CssClass = "test-class",
            IconClass = "test-icon"
        };

        Assert.Equal("test-id", option.Id);
        Assert.Equal("Test Name", option.Name);
        Assert.Equal("Test Description", option.Description);
        Assert.Equal("test-class", option.CssClass);
        Assert.Equal("test-icon", option.IconClass);
    }

    [Fact]
    public void Properties_CanBeUpdated()
    {
        var option = new ContentAreaItemOption { Id = "original" };

        option.Id = "updated";

        Assert.Equal("updated", option.Id);
    }
}
