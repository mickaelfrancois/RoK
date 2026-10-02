using Dapper;
using Microsoft.Data.Sqlite;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class TrackAnalysisRepositoryTests : IClassFixture<SqliteDatabaseFixture>
{
    private readonly SqliteDatabaseFixture _fixture;
    private readonly TrackAnalysisRepository _repository;

    public TrackAnalysisRepositoryTests(SqliteDatabaseFixture fixture)
    {
        _fixture = fixture;
        _repository = new TrackAnalysisRepository(() => new SqliteConnection(fixture.ConnectionString));
    }

    private static TrackAnalysisEntity Full(long trackId) => new()
    {
        TrackId = trackId,
        AlgorithmVersion = 3,
        FileModifiedUtc = new DateTime(2026, 5, 4, 10, 20, 30, DateTimeKind.Utc),
        FileSize = 123456,
        MusicEndSeconds = 195.25,
        FadeOutSeconds = 4.5,
        MusicStartSeconds = 2.9,
        Bpm = 123.45,
        BpmConfidence = 0.8,
        BpmSource = BpmSource.Detected,
        IntroBeatPhase = 3.1,
        OutroBeatPhase = 171.7,
        IntroTempoAnalysed = true,
        OutroTempoAnalysed = true
    };

    [Fact(DisplayName = "upsert_then_get_round_trips_every_column")]
    public async Task UpsertThenGet_RoundTripsEveryColumn()
    {
        // Arrange
        var entity = Full(1);

        // Act
        await _repository.UpsertAsync(entity, CancellationToken.None);
        var loaded = await _repository.GetAsync(1, CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(entity.TrackId, loaded.TrackId);
        Assert.Equal(entity.AlgorithmVersion, loaded.AlgorithmVersion);
        Assert.Equal(entity.FileModifiedUtc, loaded.FileModifiedUtc);
        Assert.Equal(entity.FileSize, loaded.FileSize);
        Assert.Equal(entity.MusicEndSeconds, loaded.MusicEndSeconds);
        Assert.Equal(entity.FadeOutSeconds, loaded.FadeOutSeconds);
        Assert.Equal(entity.MusicStartSeconds, loaded.MusicStartSeconds);
        Assert.Equal(entity.Bpm, loaded.Bpm);
        Assert.Equal(entity.BpmConfidence, loaded.BpmConfidence);
        Assert.Equal(BpmSource.Detected, loaded.BpmSource);
        Assert.Equal(entity.IntroBeatPhase, loaded.IntroBeatPhase);
        Assert.Equal(entity.OutroBeatPhase, loaded.OutroBeatPhase);
        Assert.True(loaded.IntroTempoAnalysed);
        Assert.True(loaded.OutroTempoAnalysed);
    }

    [Fact(DisplayName = "upsert_then_get_round_trips_null_columns_and_false_flags")]
    public async Task UpsertThenGet_RoundTripsNullColumns()
    {
        // Arrange
        var entity = new TrackAnalysisEntity
        {
            TrackId = 2,
            AlgorithmVersion = 1,
            FileModifiedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            FileSize = 10
        };

        // Act
        await _repository.UpsertAsync(entity, CancellationToken.None);
        var loaded = await _repository.GetAsync(2, CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded.MusicEndSeconds);
        Assert.Null(loaded.FadeOutSeconds);
        Assert.Null(loaded.MusicStartSeconds);
        Assert.Null(loaded.Bpm);
        Assert.Null(loaded.BpmConfidence);
        Assert.Null(loaded.BpmSource);
        Assert.Null(loaded.IntroBeatPhase);
        Assert.Null(loaded.OutroBeatPhase);
        Assert.False(loaded.IntroTempoAnalysed);
        Assert.False(loaded.OutroTempoAnalysed);
    }

    [Fact(DisplayName = "upsert_of_an_existing_row_replaces_its_values")]
    public async Task Upsert_ExistingRow_ReplacesValues()
    {
        // Arrange
        await _repository.UpsertAsync(Full(3), CancellationToken.None);
        var updated = Full(3);
        updated.Bpm = 90;
        updated.BpmSource = BpmSource.Tag;
        updated.OutroTempoAnalysed = false;

        // Act
        await _repository.UpsertAsync(updated, CancellationToken.None);

        // Assert
        var count = _fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM trackAnalysis WHERE trackId = 3");
        var loaded = await _repository.GetAsync(3, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.NotNull(loaded);
        Assert.Equal(90, loaded.Bpm);
        Assert.Equal(BpmSource.Tag, loaded.BpmSource);
        Assert.False(loaded.OutroTempoAnalysed);
    }

    [Fact(DisplayName = "get_returns_null_for_a_track_never_analysed")]
    public async Task Get_UnknownTrack_ReturnsNull()
    {
        // Act
        var loaded = await _repository.GetAsync(999, CancellationToken.None);

        // Assert
        Assert.Null(loaded);
    }

    [Fact(DisplayName = "upsert_for_a_missing_track_throws_a_foreign_key_error")]
    public async Task Upsert_MissingTrack_Throws()
    {
        // Arrange
        var entity = Full(424242);

        // Act
        var error = await Record.ExceptionAsync(() => _repository.UpsertAsync(entity, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact(DisplayName = "deleting_a_track_cascades_to_its_analysis")]
    public async Task DeleteTrack_CascadesToAnalysis()
    {
        // Arrange
        using SqliteDatabaseFixture own = new();
        var repository = new TrackAnalysisRepository(() => new SqliteConnection(own.ConnectionString));
        await repository.UpsertAsync(Full(1), CancellationToken.None);

        // Act
        own.Connection.Execute("DELETE FROM Tracks WHERE id = 1");

        // Assert
        var count = own.Connection.QuerySingle<int>("SELECT COUNT(*) FROM trackAnalysis WHERE trackId = 1");
        Assert.Equal(0, count);
    }
}