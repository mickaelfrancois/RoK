using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Merge;

namespace Rok.ApplicationTests.Features.Albums.Merge;

public class AlbumMergeRulesTests
{
    private static AlbumEntity Album(long id, string name = "Album")
    {
        return new AlbumEntity { Id = id, Name = name, AlbumPath = $"C:\\Music\\{id}" };
    }

    [Fact(DisplayName = "merge_rules_fill_empty_target_fields_and_list_conflicts")]
    public void Merge_EmptyTargetFields_AreFilledAndDifferencesListed()
    {
        // Arrange
        AlbumEntity target = Album(1);
        target.Label = "  ";
        target.Year = 0;
        target.Biography = "Target bio";
        target.GenreId = 5;
        AlbumEntity source = Album(2);
        source.Label = "Source label";
        source.Year = 1999;
        source.Biography = "Source bio";
        source.GenreId = 5;
        source.MusicBrainzID = "mbid";

        // Act
        AlbumMergeResult result = AlbumMergeRules.Merge(target, source);

        // Assert
        Assert.Equal("Source label", result.Merged.Label);
        Assert.Equal(1999, result.Merged.Year);
        Assert.Equal("mbid", result.Merged.MusicBrainzID);
        Assert.Equal("Target bio", result.Merged.Biography);
        Assert.Equal(5, result.Merged.GenreId);
        AlbumMergeFieldConflict conflict = Assert.Single(result.Conflicts);
        Assert.Equal(nameof(AlbumEntity.Biography), conflict.Field);
        Assert.Equal("Target bio", conflict.TargetValue);
        Assert.Equal("Source bio", conflict.SourceValue);
    }

    [Fact(DisplayName = "merge_rules_keep_target_identity_fields")]
    public void Merge_IdentityFields_ComeFromTheTarget()
    {
        // Arrange
        AlbumEntity target = Album(1, "Target");
        target.ArtistId = 10;
        target.IsLive = true;
        AlbumEntity source = Album(2, "Source");
        source.ArtistId = 20;
        source.IsCompilation = true;
        source.IsBestOf = true;

        // Act
        AlbumMergeResult result = AlbumMergeRules.Merge(target, source);

        // Assert
        Assert.Equal(1, result.Merged.Id);
        Assert.Equal("Target", result.Merged.Name);
        Assert.Equal(10, result.Merged.ArtistId);
        Assert.Equal("C:\\Music\\1", result.Merged.AlbumPath);
        Assert.True(result.Merged.IsLive);
        Assert.False(result.Merged.IsCompilation);
        Assert.False(result.Merged.IsBestOf);
        Assert.Empty(result.Conflicts);
    }

    [Fact(DisplayName = "merge_rules_or_favorite_and_lock_sum_listens")]
    public void Merge_Counters_AreCombined()
    {
        // Arrange
        AlbumEntity target = Album(1);
        target.ListenCount = 3;
        target.LastListen = new DateTime(2024, 1, 1);
        AlbumEntity source = Album(2);
        source.IsFavorite = true;
        source.IsLock = true;
        source.ListenCount = 4;
        source.LastListen = new DateTime(2025, 6, 1);

        // Act
        AlbumMergeResult result = AlbumMergeRules.Merge(target, source);

        // Assert
        Assert.True(result.Merged.IsFavorite);
        Assert.True(result.Merged.IsLock);
        Assert.Equal(7, result.Merged.ListenCount);
        Assert.Equal(new DateTime(2025, 6, 1), result.Merged.LastListen);
    }

    [Fact(DisplayName = "merge_rules_keep_target_when_source_year_differs")]
    public void Merge_DifferentYears_TargetWinsAndConflictIsListed()
    {
        // Arrange
        AlbumEntity target = Album(1);
        target.Year = 2001;
        AlbumEntity source = Album(2);
        source.Year = 2005;

        // Act
        AlbumMergeResult result = AlbumMergeRules.Merge(target, source);

        // Assert
        Assert.Equal(2001, result.Merged.Year);
        AlbumMergeFieldConflict conflict = Assert.Single(result.Conflicts);
        Assert.Equal(nameof(AlbumEntity.Year), conflict.Field);
    }

    [Fact(DisplayName = "for_pair_builds_the_group_with_the_target_as_survivor")]
    public void ForPair_BuildsGroup_WithTargetAsSurvivor()
    {
        // Arrange
        AlbumEntity target = Album(7, "Target");
        target.ListenCount = 2;
        AlbumEntity source = Album(3, "Source");
        source.IsFavorite = true;
        source.ListenCount = 5;
        source.LastListen = new DateTime(2025, 1, 1);

        // Act
        AlbumMergeGroup group = AlbumMergeGroup.ForPair(target, source);

        // Assert
        Assert.Equal(7, group.SurvivorId);
        Assert.Equal([3L], group.MergedIds);
        Assert.Equal("Target", group.Name);
        Assert.True(group.IsFavorite);
        Assert.Equal(7, group.ListenCount);
        Assert.Equal(new DateTime(2025, 1, 1), group.LastListen);
    }
}