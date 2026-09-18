using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;
using Rok.WebApi.Contracts;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class PlaylistsRouteHandlerTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerCommandService> _commandService = new();
    private readonly Mock<ILogger<PlaylistsRouteHandler>> _logger = new();

    private PlaylistsRouteHandler BuildHandler() =>
        new(_mediator, _commandService.Object, action => action(), _logger.Object);

    [Fact(DisplayName = "Listing the playlists should sort them by name, like the desktop list does")]
    public async Task ListPlaylists_ShouldSortByName()
    {
        // Arrange
        List<PlaylistHeaderDto> playlists =
        [
            new() { Id = 3, Name = "Punk's not dead" },
            new() { Id = 7, Name = "Vacances 2017" },
            new() { Id = 1, Name = "Ambiance" }
        ];

        _mediator.Setup<GetAllPlaylistsRequest, IEnumerable<PlaylistHeaderDto>>().Returns(playlists);

        PlaylistsRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/playlists");

        // Assert
        List<PlaylistSummary>? summaries =
            JsonSerializer.Deserialize(result.Body, RokJsonContext.Default.ListPlaylistSummary);

        Assert.NotNull(summaries);
        Assert.Equal(["Ambiance", "Punk's not dead", "Vacances 2017"], summaries.Select(summary => summary.Name));
    }
}