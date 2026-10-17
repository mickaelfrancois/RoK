using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Albums.Compilation;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class AlbumCompilationRepositoryTests
{
    private const long AlbumId = 1;
    private const long ArtistId = 1;
    private const long GenreId = 1;

    private static AlbumCompilationRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, new PendingTagWriteRepository(fixture.Connection, NullLogger<PendingTagWriteRepository>.Instance), TimeProvider.System, NullLogger<AlbumCompilationRepository>.Instance);

    private static AlbumCompilationDbChange BuildChange(bool isCompilation, params PendingTagWrite[] pending) =>
        new(AlbumId, isCompilation, ArtistId, GenreId, pending);

    private static Task<int> CompilationCountOfArtistAsync(SqliteDatabaseFixture fixture) =>
        fixture.Connection.ExecuteScalarAsync<int>("SELECT compilationCount FROM artists WHERE id = @ArtistId", new { ArtistId });

    private static Task<int> CompilationCountOfGenreAsync(SqliteDatabaseFixture fixture) =>
        fixture.Connection.ExecuteScalarAsync<int>("SELECT compilationCount FROM genres WHERE id = @GenreId", new { GenreId });

    [Fact(DisplayName = "set_compilation_stores_status_manual_origin_pending_rows_and_counters")]
    public async Task ApplyAsync_Check_StoresStatusOriginPendingRowsAndCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumCompilationRepository repository = CreateRepository(fixture);
        int before = await CompilationCountOfArtistAsync(fixture);
        int genreBefore = await CompilationCountOfGenreAsync(fixture);

        // Act
        await repository.ApplyAsync(BuildChange(true, new PendingTagWrite(1, ETagWriteFields.Compilation), new PendingTagWrite(2, ETagWriteFields.Compilation)));

        // Assert
        (bool isCompilation, int source) = await fixture.Connection.QuerySingleAsync<(bool, int)>("SELECT isCompilation, compilationSource FROM albums WHERE id = @AlbumId", new { AlbumId });
        Assert.True(isCompilation);
        Assert.Equal((int)ECompilationSource.Manual, source);
        Assert.Equal(before + 1, await CompilationCountOfArtistAsync(fixture));
        Assert.Equal(genreBefore + 1, await CompilationCountOfGenreAsync(fixture));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites WHERE fields = @fields", new { fields = (int)ETagWriteFields.Compilation }));
    }

    [Fact(DisplayName = "set_compilation_uncheck_lowers_the_counters_again")]
    public async Task ApplyAsync_CheckThenUncheck_RestoresCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumCompilationRepository repository = CreateRepository(fixture);
        int before = await CompilationCountOfArtistAsync(fixture);
        int genreBefore = await CompilationCountOfGenreAsync(fixture);
        await repository.ApplyAsync(BuildChange(true));

        // Act
        await repository.ApplyAsync(BuildChange(false));

        // Assert
        Assert.Equal(before, await CompilationCountOfArtistAsync(fixture));
        Assert.Equal(genreBefore, await CompilationCountOfGenreAsync(fixture));
        Assert.Equal((int)ECompilationSource.Manual, await fixture.Connection.ExecuteScalarAsync<int>("SELECT compilationSource FROM albums WHERE id = @AlbumId", new { AlbumId }));
    }

    [Fact(DisplayName = "set_compilation_without_pending_writes_adds_no_pending_row")]
    public async Task ApplyAsync_NoPendingWrites_AddsNoPendingRow()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumCompilationRepository repository = CreateRepository(fixture);

        // Act
        await repository.ApplyAsync(BuildChange(true));

        // Assert
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "set_compilation_rolls_everything_back_for_an_unknown_album")]
    public async Task ApplyAsync_UnknownAlbum_ThrowsAndRollsBack()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumCompilationRepository repository = CreateRepository(fixture);
        AlbumCompilationDbChange change = new(999, true, ArtistId, GenreId, [new PendingTagWrite(1, ETagWriteFields.Compilation)]);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ApplyAsync(change));

        // Assert
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "set_compilation_rolls_back_the_status_when_a_pending_row_fails")]
    public async Task ApplyAsync_PendingRowFails_RollsBackTheStatus()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumCompilationRepository repository = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("CREATE TRIGGER fail_pending BEFORE INSERT ON pendingTagWrites BEGIN SELECT RAISE(ABORT, 'boom'); END");

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => repository.ApplyAsync(BuildChange(true, new PendingTagWrite(1, ETagWriteFields.Compilation))));

        // Assert
        Assert.False(await fixture.Connection.ExecuteScalarAsync<bool>("SELECT isCompilation FROM albums WHERE id = @AlbumId", new { AlbumId }));
        Assert.Equal((int)ECompilationSource.Derived, await fixture.Connection.ExecuteScalarAsync<int>("SELECT compilationSource FROM albums WHERE id = @AlbumId", new { AlbumId }));
    }
}