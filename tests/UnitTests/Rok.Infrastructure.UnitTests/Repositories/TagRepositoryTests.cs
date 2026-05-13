using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class TagRepositoryTests
{
    private static TagRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<TagRepository>.Instance, TimeProvider.System);

    [Fact(DisplayName = "UpdateEntityTags should create new tags and link them to the entity")]
    public async Task UpdateEntityTags_ShouldCreateNewTags_AndLinkThemToEntity()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);

        // Act
        bool ok = await repo.UpdateEntityTagsAsync(1, new[] { "rock", "pop" }, "albumtags", "albumid");

        // Assert
        Assert.True(ok);
        int tagCount = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM tags");
        Assert.Equal(2, tagCount);
        int linkCount = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM albumtags WHERE albumid = 1");
        Assert.Equal(2, linkCount);
    }

    [Fact(DisplayName = "UpdateEntityTags should reuse existing tags when name already exists")]
    public async Task UpdateEntityTags_ShouldReuseExistingTags_WhenNameAlreadyExists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("INSERT INTO tags(name) VALUES ('rock'), ('jazz')");

        // Act
        await repo.UpdateEntityTagsAsync(1, new[] { "rock", "blues" }, "albumtags", "albumid");

        // Assert
        int tagCount = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM tags");
        Assert.Equal(3, tagCount);
    }

    [Fact(DisplayName = "UpdateEntityTags should replace previous links with the new set")]
    public async Task UpdateEntityTags_ShouldReplacePreviousLinks_WithNewSet()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);
        await repo.UpdateEntityTagsAsync(1, new[] { "rock", "pop" }, "albumtags", "albumid");

        // Act
        await repo.UpdateEntityTagsAsync(1, new[] { "jazz" }, "albumtags", "albumid");

        // Assert
        List<string> remaining = (await fixture.Connection.QueryAsync<string>(
            "SELECT tags.name FROM albumtags JOIN tags ON tags.id = albumtags.tagid WHERE albumtags.albumid = 1")).ToList();
        Assert.Single(remaining);
        Assert.Equal("jazz", remaining[0]);
    }

    [Fact(DisplayName = "UpdateEntityTags should trim deduplicate and ignore blank tag names")]
    public async Task UpdateEntityTags_ShouldTrimDeduplicate_AndIgnoreBlankTagNames()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);

        // Act
        await repo.UpdateEntityTagsAsync(1, new[] { "  rock  ", "rock", "", "   " }, "albumtags", "albumid");

        // Assert
        List<string> tags = (await fixture.Connection.QueryAsync<string>("SELECT name FROM tags")).ToList();
        Assert.Single(tags);
        Assert.Equal("rock", tags[0]);
    }

    [Fact(DisplayName = "UpdateEntityTags should clear previous tags when input is empty")]
    public async Task UpdateEntityTags_ShouldClearPreviousTags_WhenInputIsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);
        await repo.UpdateEntityTagsAsync(1, new[] { "rock" }, "albumtags", "albumid");

        // Act
        bool ok = await repo.UpdateEntityTagsAsync(1, Array.Empty<string>(), "albumtags", "albumid");

        // Assert
        Assert.True(ok);
        int linkCount = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM albumtags WHERE albumid = 1");
        Assert.Equal(0, linkCount);
    }

    [Fact(DisplayName = "GetAll should return every tag in the repository")]
    public async Task GetAll_ShouldReturnEveryTag_InRepository()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TagRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("INSERT INTO tags(name) VALUES ('rock'), ('pop')");

        // Act
        List<TagEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }
}
