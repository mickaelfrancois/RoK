using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Terminal;

namespace Rok.PresentationTests.Services.PlayerCommand.Terminal;

public class PlayerCommandHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private PlayerCommandHandler BuildHandler() => new(_commandService.Object, NullLogger<PlayerCommandHandler>.Instance);

    [Theory(DisplayName = "HandleAsync should ignore null or whitespace arguments")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_ShouldIgnoreEmptyArguments(string? arguments)
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync(arguments);

        // Assert
        _commandService.VerifyNoOtherCalls();
    }

    [Theory(DisplayName = "HandleAsync should dispatch primitive commands to the right method")]
    [InlineData("rok play", nameof(IPlayerCommandService.Play))]
    [InlineData("rok pause", nameof(IPlayerCommandService.Pause))]
    [InlineData("rok toggle", nameof(IPlayerCommandService.Toggle))]
    [InlineData("rok next", nameof(IPlayerCommandService.Next))]
    [InlineData("rok prev", nameof(IPlayerCommandService.Previous))]
    [InlineData("rok previous", nameof(IPlayerCommandService.Previous))]
    [InlineData("rok mute", nameof(IPlayerCommandService.ToggleMute))]
    public async Task HandleAsync_ShouldDispatchPrimitiveCommands(string command, string expectedMethod)
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync(command);

        // Assert
        switch (expectedMethod)
        {
            case nameof(IPlayerCommandService.Play):
                _commandService.Verify(c => c.Play(), Times.Once); break;
            case nameof(IPlayerCommandService.Pause):
                _commandService.Verify(c => c.Pause(), Times.Once); break;
            case nameof(IPlayerCommandService.Toggle):
                _commandService.Verify(c => c.Toggle(), Times.Once); break;
            case nameof(IPlayerCommandService.Next):
                _commandService.Verify(c => c.Next(), Times.Once); break;
            case nameof(IPlayerCommandService.Previous):
                _commandService.Verify(c => c.Previous(), Times.Once); break;
            case nameof(IPlayerCommandService.ToggleMute):
                _commandService.Verify(c => c.ToggleMute(), Times.Once); break;
        }
    }

    [Fact(DisplayName = "HandleAsync should set volume when given a parsable number")]
    public async Task HandleAsync_ShouldSetVolume_WhenNumberProvided()
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync("rok volume 75");

        // Assert
        _commandService.Verify(c => c.SetVolume(75d), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should not set volume when value is not parsable")]
    public async Task HandleAsync_ShouldNotSetVolume_WhenNotANumber()
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync("rok volume abc");

        // Assert
        _commandService.Verify(c => c.SetVolume(It.IsAny<double>()), Times.Never);
    }

    [Theory(DisplayName = "HandleAsync should dispatch listen sub-commands")]
    [InlineData("rok listen album \"Best Of\"", "album")]
    [InlineData("rok listen artist Beatles", "artist")]
    [InlineData("rok listen genre Rock", "genre")]
    [InlineData("rok listen playlist MyMix", "playlist")]
    public async Task HandleAsync_ShouldDispatchListenSubCommands(string arguments, string sub)
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync(arguments);

        // Assert
        switch (sub)
        {
            case "album":
                _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Once);
                _commandService.Verify(c => c.ListenArtistAsync(It.IsAny<string>()), Times.Never);
                break;
            case "artist":
                _commandService.Verify(c => c.ListenArtistAsync(It.IsAny<string>()), Times.Once);
                _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Never);
                break;
            case "genre":
                _commandService.Verify(c => c.ListenGenreAsync(It.IsAny<string>()), Times.Once);
                _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Never);
                break;
            case "playlist":
                _commandService.Verify(c => c.ListenPlaylistAsync(It.IsAny<string>()), Times.Once);
                _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Never);
                break;
        }
    }

    [Fact(DisplayName = "HandleAsync should ignore unknown commands")]
    public async Task HandleAsync_ShouldIgnoreUnknownCommands()
    {
        // Arrange
        PlayerCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync("rok doesnotexist arg");

        // Assert
        _commandService.VerifyNoOtherCalls();
    }
}
