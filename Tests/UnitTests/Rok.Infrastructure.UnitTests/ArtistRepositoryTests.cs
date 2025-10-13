using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Domain.Interfaces.Entities;
using Rok.Infrastructure.Repositories;
using Xunit;

namespace Rok.Infrastructure.UnitTests;

public class ArtistRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private ArtistRepository CreateRepository()
    {
        return new ArtistRepository(fixture.Connection, fixture.Connection, NullLogger<ArtistRepository>.Instance);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingArtists()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        // Act
        var result = (await repo.SearchAsync("Artist A")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Artist A", result[0].Name);
    }

    [Fact]
    public async Task GetByGenreIdAsync_ReturnsGenreArtists()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        // Ensure artists have a genreId for the test
        await fixture.Connection.ExecuteAsync("UPDATE Artists SET genreId = @g WHERE id IN (1,2)", new { g = 1 });

        // Act
        List<IArtistEntity> result = (await repo.GetByGenreIdAsync(1)).ToList();

        // Assert
        Assert.True(result.Count >= 2);
        Assert.Contains(result, a => a.Id == 1);
        Assert.Contains(result, a => a.Id == 2);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavorite()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateFavoriteAsync(1, true);

        // Assert
        Assert.True(ok);

        ArtistEntity? artist = await repo.GetByIdAsync(1);
        Assert.NotNull(artist);
        Assert.True(artist!.IsFavorite);
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        var before = await repo.GetByIdAsync(1);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(1);

        // Assert
        Assert.True(ok);

        var after = await repo.GetByIdAsync(1);
        Assert.NotNull(after);
        Assert.Equal(beforeCount + 1, after!.ListenCount);
        Assert.NotNull(after.LastListen);
        Assert.True((DateTime.UtcNow - after.LastListen.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateStatisticsAsync_UpdatesFields()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateStatisticsAsync(1, trackCount: 42, totalDurationSeconds: 12345, albumCount: 7, bestOfCount: 1, liveCount: 2, compilationCount: 3, yearMini: 1990, yearMaxi: 2020);

        // Assert
        Assert.True(ok);

        ArtistEntity? artist = await repo.GetByIdAsync(1);
        Assert.NotNull(artist);
        Assert.Equal(42, artist!.TrackCount);
        Assert.Equal(12345, artist.TotalDurationSeconds);
        Assert.Equal(7, artist.AlbumCount);
        Assert.Equal(1, artist.BestofCount);
        Assert.Equal(2, artist.LiveCount);
        Assert.Equal(3, artist.CompilationCount);
    }

    [Fact]
    public async Task DeleteArtistsWithoutTracks_RemovesArtistsWithoutTracks()
    {
        // Arrange
        ArtistRepository repo = CreateRepository();

        // Insert an artist without tracks (must provide NOT NULL fields)
        DateTime now = DateTime.UtcNow;
        await fixture.Connection.ExecuteAsync(@"
                            INSERT INTO Artists(
                                id, name, wikipediaUrl, officialSiteUrl, facebookUrl, twitterUrl, novaUid, musicBrainzID,
                                yearMini, yearMaxi, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds,
                                formedYear, bornYear, diedYear, disbanded, style, gender, mood, members, similarArtists, biography,
                                countryId, genreId, isFavorite, listenCount, lastListen, creatDate
                            ) VALUES (
                                @id, @name, NULL, NULL, NULL, NULL, NULL, NULL,
                                NULL, NULL, 0, 0, 0, 0, 0, 0,
                                NULL, NULL, NULL, 0, NULL, NULL, NULL, NULL, NULL, NULL,
                                NULL, NULL, 0, 0, NULL, @now
                            )", new { id = 9999, name = "Orphan Artist", now });

        // Act
        int deleted = await repo.DeleteArtistsWithoutTracks();

        // Assert
        Assert.True(deleted >= 1);

        List<ArtistEntity> remaining = (await repo.GetAllAsync()).ToList();
        Assert.DoesNotContain(remaining, a => a.Id == 9999);
    }
}