using Moq;
using Rok.Application.Features.Tags.Query;
using Rok.Application.Interfaces;

namespace Rok.ApplicationTests.Features.Tags;

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
        GetAllTagsQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<TagDto> result = await handler.HandleAsync(new GetAllTagsQuery(), CancellationToken.None);

        // Assert
        List<TagDto> tags = result.ToList();
        Assert.Equal(2, tags.Count);
        Assert.Equal("rock", tags[0].Name);
        Assert.Equal(2, tags[1].Id);
    }
}
