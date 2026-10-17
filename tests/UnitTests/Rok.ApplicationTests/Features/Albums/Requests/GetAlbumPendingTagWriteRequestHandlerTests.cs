using CleanArch.DevKit.Mediator.Results;
using Moq;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Albums.Requests;

public class GetAlbumPendingTagWriteRequestHandlerTests
{
    private readonly Mock<IPendingTagWriteRepository> _repository = new();

    [Theory(DisplayName = "an_album_is_pending_only_when_at_least_one_track_has_a_pending_row")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public async Task Handle_ReportsWhetherTracksArePending(int count, bool expected)
    {
        // Arrange
        _repository.Setup(r => r.CountByAlbumAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(count);
        GetAlbumPendingTagWriteRequestHandler sut = new(_repository.Object);

        // Act
        Result<bool> result = await sut.Handle(new GetAlbumPendingTagWriteRequest(7), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }
}