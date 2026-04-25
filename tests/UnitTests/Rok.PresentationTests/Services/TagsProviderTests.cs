using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tags.Query;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class TagsProviderTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact(DisplayName = "GetTagsAsync should load tags from the mediator on first call")]
    public async Task GetTagsAsync_ShouldLoadTagsOnFirstCall()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllTagsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TagDto> { new() { Name = "rock" }, new() { Name = "jazz" } });
        using TagsProvider sut = new(_mediator.Object);

        // Act
        List<string> result = await sut.GetTagsAsync();

        // Assert
        Assert.Equal(new[] { "jazz", "rock" }, result.ToArray());
    }

    [Fact(DisplayName = "GetTagsAsync should not reload tags on subsequent calls")]
    public async Task GetTagsAsync_ShouldNotReloadTags_AfterFirstCall()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllTagsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TagDto> { new() { Name = "rock" } });
        using TagsProvider sut = new(_mediator.Object);

        // Act
        await sut.GetTagsAsync();
        await sut.GetTagsAsync();

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllTagsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetTagsAsync should remove duplicates and order tags alphabetically")]
    public async Task GetTagsAsync_ShouldDeduplicateAndOrder()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllTagsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TagDto>
                 {
                     new() { Name = "rock" },
                     new() { Name = "jazz" },
                     new() { Name = "rock" },
                     new() { Name = "ambient" }
                 });
        using TagsProvider sut = new(_mediator.Object);

        // Act
        List<string> result = await sut.GetTagsAsync();

        // Assert
        Assert.Equal(new[] { "ambient", "jazz", "rock" }, result.ToArray());
    }

    [Fact(DisplayName = "Dispose should be idempotent")]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        TagsProvider sut = new(_mediator.Object);

        // Act
        sut.Dispose();
        sut.Dispose();

        // Assert — no exception
    }
}
