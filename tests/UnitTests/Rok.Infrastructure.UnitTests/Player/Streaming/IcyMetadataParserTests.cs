using Rok.Infrastructure.Player.Streaming;

namespace Rok.Infrastructure.UnitTests.Player.Streaming;

public class IcyMetadataParserTests
{
    [Fact(DisplayName = "Parse should extract StreamTitle from a well-formed metadata block")]
    public void Parse_ShouldExtractStreamTitle_FromWellFormedBlock()
    {
        // Arrange
        string block = "StreamTitle='Daft Punk - One More Time';StreamUrl='http://example/';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Equal("Daft Punk - One More Time", title);
    }

    [Fact(DisplayName = "Parse should return null when StreamTitle is missing")]
    public void Parse_ShouldReturnNull_WhenStreamTitleIsMissing()
    {
        // Arrange
        string block = "StreamUrl='http://example/';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Null(title);
    }

    [Fact(DisplayName = "Parse should return null on malformed input")]
    public void Parse_ShouldReturnNull_OnMalformedInput()
    {
        // Arrange
        string block = "StreamTitle='Unterminated";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Null(title);
    }

    [Fact(DisplayName = "Parse should handle empty StreamTitle")]
    public void Parse_ShouldHandle_EmptyStreamTitle()
    {
        // Arrange
        string block = "StreamTitle='';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Equal(string.Empty, title);
    }
}
