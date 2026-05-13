using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class EqualizerPresetRepositoryTests
{
    private static EqualizerPresetRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<EqualizerPresetRepository>.Instance, TimeProvider.System);

    private static float[] BuildBands(float value = 1.5f)
    {
        float[] bands = new float[10];
        for (int i = 0; i < 10; i++) bands[i] = value + i;
        return bands;
    }

    [Fact(DisplayName = "Save should insert a new preset when none exists for the scope")]
    public async Task Save_ShouldInsertNewPreset_WhenNoneExistsForScope()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        EqualizerPresetEntity preset = new() { Scope = EqualizerScope.Track, ScopeId = 42, Bands = BuildBands() };

        // Act
        await repo.SaveAsync(preset);

        // Assert
        int count = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EqualizerPreset");
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "Save should replace existing preset for the same scope and scope id")]
    public async Task Save_ShouldReplaceExistingPreset_ForSameScopeAndScopeId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Album, ScopeId = 1, Bands = BuildBands(1) });

        // Act
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Album, ScopeId = 1, Bands = BuildBands(9) });

        // Assert
        int count = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EqualizerPreset");
        Assert.Equal(1, count);
        EqualizerPresetEntity? loaded = await repo.FindAsync(EqualizerScope.Album, 1);
        Assert.NotNull(loaded);
        Assert.Equal(9f, loaded!.Bands[0]);
    }

    [Fact(DisplayName = "Save should encode builtin preset key as Default_Key scope")]
    public async Task Save_ShouldEncodeBuiltinPresetKey_AsDefaultKeyScope()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        EqualizerPresetEntity preset = new() { Scope = EqualizerScope.Default, BuiltinPresetKey = "Rock", Bands = BuildBands() };

        // Act
        await repo.SaveAsync(preset);

        // Assert
        string? scope = await fixture.Connection.ExecuteScalarAsync<string>("SELECT Scope FROM EqualizerPreset");
        Assert.Equal("Default_Rock", scope);
    }

    [Fact(DisplayName = "Find should return null when no preset matches scope and scope id")]
    public async Task Find_ShouldReturnNull_WhenNoPresetMatchesScopeAndScopeId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);

        // Act
        EqualizerPresetEntity? result = await repo.FindAsync(EqualizerScope.Track, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Find should decode Default_Key scope back into BuiltinPresetKey")]
    public async Task Find_ShouldDecodeDefaultKeyScope_BackIntoBuiltinPresetKey()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Default, BuiltinPresetKey = "Jazz", Bands = BuildBands() });

        // Act
        EqualizerPresetEntity? result = await repo.FindAsync(EqualizerScope.Default, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EqualizerScope.Default, result!.Scope);
        Assert.Equal("Jazz", result.BuiltinPresetKey);
    }

    [Fact(DisplayName = "Find should return matching preset for non-default scope")]
    public async Task Find_ShouldReturnMatchingPreset_ForNonDefaultScope()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Artist, ScopeId = 7, Bands = BuildBands(2f) });

        // Act
        EqualizerPresetEntity? result = await repo.FindAsync(EqualizerScope.Artist, 7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EqualizerScope.Artist, result!.Scope);
        Assert.Equal(7, result.ScopeId);
    }

    [Fact(DisplayName = "Delete should remove the preset matching scope and scope id")]
    public async Task Delete_ShouldRemovePreset_MatchingScopeAndScopeId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Genre, ScopeId = 3, Bands = BuildBands() });

        // Act
        await repo.DeleteAsync(EqualizerScope.Genre, 3);

        // Assert
        Assert.Null(await repo.FindAsync(EqualizerScope.Genre, 3));
    }

    [Fact(DisplayName = "Delete should remove default preset regardless of builtin key")]
    public async Task Delete_ShouldRemoveDefaultPreset_RegardlessOfBuiltinKey()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        EqualizerPresetRepository repo = CreateRepository(fixture);
        await repo.SaveAsync(new EqualizerPresetEntity { Scope = EqualizerScope.Default, BuiltinPresetKey = "Pop", Bands = BuildBands() });

        // Act
        await repo.DeleteAsync(EqualizerScope.Default, null);

        // Assert
        Assert.Null(await repo.FindAsync(EqualizerScope.Default, null));
    }
}
