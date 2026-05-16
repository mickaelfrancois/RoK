using Moq;
using Rok.Application.Features.Insights;
using Rok.Application.Features.Insights.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Insights.Requests;

public class GetInsightsQueryHandlerTests
{
    [Fact(DisplayName = "Handle should delegate to repository and return its insights")]
    public async Task Handle_ShouldDelegateToRepository_AndReturnItsInsights()
    {
        // Arrange
        DateTime month = new(2026, 4, 1);
        InsightsDto expected = new() { SessionCount = 10, TracksPlayed = 42 };
        Mock<IListeningEventRepository> repository = new();
        repository.Setup(r => r.GetInsightsAsync(month)).ReturnsAsync(expected);
        GetInsightsRequestHandler handler = new(repository.Object);

        // Act
        InsightsDto result = await handler.Handle(new GetInsightsRequest { Month = month }, CancellationToken.None);

        // Assert
        Assert.Same(expected, result);
        repository.Verify(r => r.GetInsightsAsync(month), Times.Once);
    }
}
