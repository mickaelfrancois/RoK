using CleanArch.DevKit.Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Pipeline;

namespace Rok.ApplicationTests.Pipeline;

public sealed record FakeRequest(int Value) : IRequest<int>;

public sealed class FakeRequestHandler : IRequestHandler<FakeRequest, int>
{
    public Task<int> Handle(FakeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

public class LoggingPipelineBehaviorTests
{

    [Fact(DisplayName = "logs_request_name_and_returns_next_result")]
    public async Task logs_request_name_and_returns_next_result()
    {
        Mock<ILogger<LoggingPipelineBehavior<FakeRequest, int>>> loggerMock = new();
        LoggingPipelineBehavior<FakeRequest, int> sut = new(loggerMock.Object);
        FakeRequest request = new(42);
        RequestHandlerDelegate<int> next = _ => Task.FromResult(99);

        int result = await sut.Handle(request, next, CancellationToken.None);

        Assert.Equal(99, result);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("FakeRequest")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "rethrows_and_logs_when_next_throws")]
    public async Task rethrows_and_logs_when_next_throws()
    {
        Mock<ILogger<LoggingPipelineBehavior<FakeRequest, int>>> loggerMock = new();
        LoggingPipelineBehavior<FakeRequest, int> sut = new(loggerMock.Object);
        FakeRequest request = new(42);
        InvalidOperationException expected = new("boom");
        RequestHandlerDelegate<int> next = _ => throw expected;

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(request, next, CancellationToken.None));

        Assert.Same(expected, actual);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                expected,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
