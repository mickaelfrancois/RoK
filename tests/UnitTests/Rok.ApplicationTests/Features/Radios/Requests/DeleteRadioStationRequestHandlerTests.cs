using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class DeleteRadioStationRequestHandlerTests
{
    [Fact(DisplayName = "Handle should delete station and return success")]
    public async Task Handle_ShouldDeleteStation_AndReturnSuccess()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(5L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RadioStationEntity { Id = 5, Name = "X", StreamUrl = "http://x/" });
        DeleteRadioStationRequestHandler handler = new(repo.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteRadioStationRequest { Id = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repo.Verify(r => r.DeleteAsync(5L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return NotFound when station does not exist")]
    public async Task Handle_ShouldReturnNotFound_WhenStationDoesNotExist()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RadioStationEntity?)null);
        DeleteRadioStationRequestHandler handler = new(repo.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteRadioStationRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found");
        repo.Verify(r => r.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}