using Rok.Application.Dto;
using Rok.Application.Features.Tags.Requests;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class TagsProviderTests
{
    private readonly FakeMediator _mediator = new();

    [Fact(DisplayName = "GetTagsAsync should load tags from the mediator on first call")]
    public async Task GetTagsAsync_ShouldLoadTagsOnFirstCall()
    {
        // Arrange
        _mediator.Setup<GetAllTagsRequest, IEnumerable<TagDto>>()
                 .Returns(new List<TagDto> { new() { Name = "rock" }, new() { Name = "jazz" } });
        using TagsProvider sut = new(_mediator, new Messenger());

        // Act
        List<string> result = await sut.GetTagsAsync();

        // Assert
        Assert.Equal(new[] { "jazz", "rock" }, result.ToArray());
    }

    [Fact(DisplayName = "GetTagsAsync should not reload tags on subsequent calls")]
    public async Task GetTagsAsync_ShouldNotReloadTags_AfterFirstCall()
    {
        // Arrange
        _mediator.Setup<GetAllTagsRequest, IEnumerable<TagDto>>()
                 .Returns(new List<TagDto> { new() { Name = "rock" } });
        using TagsProvider sut = new(_mediator, new Messenger());

        // Act
        await sut.GetTagsAsync();
        await sut.GetTagsAsync();

        // Assert
        Assert.Single(_mediator.Sent<GetAllTagsRequest>());
    }

    [Fact(DisplayName = "GetTagsAsync should remove duplicates and order tags alphabetically")]
    public async Task GetTagsAsync_ShouldDeduplicateAndOrder()
    {
        // Arrange
        _mediator.Setup<GetAllTagsRequest, IEnumerable<TagDto>>()
                 .Returns(new List<TagDto>
                 {
                     new() { Name = "rock" },
                     new() { Name = "jazz" },
                     new() { Name = "rock" },
                     new() { Name = "ambient" }
                 });
        using TagsProvider sut = new(_mediator, new Messenger());

        // Act
        List<string> result = await sut.GetTagsAsync();

        // Assert
        Assert.Equal(new[] { "ambient", "jazz", "rock" }, result.ToArray());
    }

    [Fact(DisplayName = "Dispose should be idempotent")]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        TagsProvider sut = new(_mediator, new Messenger());

        // Act
        sut.Dispose();
        sut.Dispose();

        // Assert — no exception
    }
}
