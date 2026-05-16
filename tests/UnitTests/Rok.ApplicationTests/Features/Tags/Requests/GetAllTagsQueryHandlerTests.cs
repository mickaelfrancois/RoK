using Moq;
using Rok.Application.Features.Tags.Requests;
using Rok.Application.Interfaces;

namespace Rok.ApplicationTests.Features.Tags.Requests;

public class GetAllTagsQueryHandlerTests
{
    [Fact(DisplayName = "Handle should map all tag entities returned by repository to DTOs")]
    public async Task Handle_ShouldMapAllTagEntities_ReturnedByRepository_ToDtos()
    {
        // Arrange
        List<TagEntity> entities = new()
        {
            new() { Id = 1, Name = "rock" },
            new() { Id = 2, Name = "pop" }
        };
        Mock<ITagRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entities);
        GetAllTagsRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TagDto> result = await handler.Handle(new GetAllTagsRequest(), CancellationToken.None);

        // Assert
        List<TagDto> tags = result.ToList();
        Assert.Equal(2, tags.Count);
        Assert.Equal("rock", tags[0].Name);
        Assert.Equal(2, tags[1].Id);
    }
}