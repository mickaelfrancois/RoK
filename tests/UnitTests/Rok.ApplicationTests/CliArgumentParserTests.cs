using Rok.Application.PlayerCommand.Terminal;

namespace Rok.ApplicationTests;

public class CliArgumentParserTests
{
    [Fact]
    public void GetCommand_ShouldParseArgumentsCorrectly()
    {
        // Arrange
        string input = @"""C:\Program Files\Rok\Rok.exe"" play ""My Favorite Playlist""";

        // Act
        string[] result = CliArgumentParser.Parse(input);

        // Assert
        Assert.Equal(new[] { "play", "My Favorite Playlist" }, result);
    }

    [Fact]
    public void GetCommand_ShouldHandleEmptyArguments()
    {
        // Arrange
        string input = @"""C:\Program Files\Rok\Rok.exe""";

        // Act
        string[] result = CliArgumentParser.Parse(input);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetCommand_ShouldHandleArgumentsWithoutQuotes()
    {
        // Arrange
        string input = @"""C:\Program Files\Rok\Rok.exe"" play MyFavoritePlaylist";

        // Act
        string[] result = CliArgumentParser.Parse(input);

        // Assert
        Assert.Equal(new[] { "play", "MyFavoritePlaylist" }, result);
    }
}
