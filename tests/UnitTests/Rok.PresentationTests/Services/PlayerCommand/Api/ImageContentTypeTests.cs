using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class ImageContentTypeTests
{
    [Theory(DisplayName = "FromPath should map known image extensions and fall back for the rest")]
    [InlineData(@"C:\a\cover.jpg", "image/jpeg")]
    [InlineData(@"C:\a\cover.jpeg", "image/jpeg")]
    [InlineData(@"C:\a\cover.png", "image/png")]
    [InlineData(@"C:\a\cover.webp", "image/webp")]
    [InlineData(@"C:\a\COVER.JPG", "image/jpeg")]
    [InlineData(@"C:\a\cover.gif", "application/octet-stream")]
    [InlineData(@"C:\a\cover", "application/octet-stream")]
    public void FromPath_ShouldMapExtension(string path, string expected)
    {
        // Act
        string result = ImageContentType.FromPath(path);

        // Assert
        Assert.Equal(expected, result);
    }
}