using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class GetRadioStationsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped DTOs in repository order")]
    public async Task Handle_ShouldReturnMappedDtos_InRepositoryOrder()
    {
        // Arrange
        List<RadioStationEntity> entities =
        [
            new() { Id = 1, Name = "A", StreamUrl = "http://a/", AddedAt = DateTime.UtcNow },
            new() { Id = 2, Name = "B", StreamUrl = "http://b/", AddedAt = DateTime.UtcNow }
        ];
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);
        GetRadioStationsRequestHandler handler = new(repo.Object);

        // Act
        Result<IReadOnlyList<RadioStationDto>> result = await handler.Handle(new GetRadioStationsRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("A", result.Value[0].Name);
    }

    [Fact(DisplayName = "Handle should return empty list when repository returns none")]
    public async Task Handle_ShouldReturnEmptyList_WhenRepositoryReturnsNone()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        GetRadioStationsRequestHandler handler = new(repo.Object);

        // Act
        Result<IReadOnlyList<RadioStationDto>> result = await handler.Handle(new GetRadioStationsRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Empty(result.Value);
    }
}
