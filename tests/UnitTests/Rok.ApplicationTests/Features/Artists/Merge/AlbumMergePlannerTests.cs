using Rok.Application.Features.Artists.Merge;

namespace Rok.ApplicationTests.Features.Artists.Merge;

public class AlbumMergePlannerTests
{
    private static AlbumEntity Album(long id, string name, bool compilation = false, bool favorite = false, int listens = 0, DateTime? lastListen = null)
    {
        return new AlbumEntity { Id = id, Name = name, IsCompilation = compilation, IsFavorite = favorite, ListenCount = listens, LastListen = lastListen };
    }

    [Fact(DisplayName = "album_planner_groups_homonyms_like_the_import_key")]
    public void Plan_Homonyms_AreGroupedLikeTheImportKey()
    {
        // Arrange
        AlbumEntity[] target = [Album(10, "Back in Black")];
        AlbumEntity[] source = [Album(20, "back in black"), Album(21, "Live – Tour"), Album(22, "Other")];
        AlbumEntity[] targetDash = [Album(11, "Live - Tour")];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan([.. target, .. targetDash], source);

        // Assert
        Assert.Equal(2, plan.Count);
        AlbumMergeGroup black = Assert.Single(plan, g => g.SurvivorId == 10);
        Assert.Equal([20L], black.MergedIds);
        AlbumMergeGroup live = Assert.Single(plan, g => g.SurvivorId == 11);
        Assert.Equal([21L], live.MergedIds);
    }

    [Fact(DisplayName = "album_planner_keeps_the_target_album_and_ignores_compilations")]
    public void Plan_Survivor_IsTheTargetAlbum_AndCompilationsAreIgnored()
    {
        // Arrange
        AlbumEntity[] target = [Album(50, "Hits"), Album(60, "Greatest", compilation: true)];
        AlbumEntity[] source = [Album(5, "Hits"), Album(6, "Greatest", compilation: true)];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan(target, source);

        // Assert
        AlbumMergeGroup group = Assert.Single(plan);
        Assert.Equal(50, group.SurvivorId);
        Assert.Equal([5L], group.MergedIds);
        Assert.Equal("Hits", group.Name);
    }

    [Fact(DisplayName = "album_planner_merges_duplicates_inside_the_source")]
    public void Plan_DuplicatesInsideTheSource_KeepTheLowestId()
    {
        // Arrange
        AlbumEntity[] source = [Album(9, "Twin"), Album(4, "TWIN"), Album(7, "Alone")];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan([], source);

        // Assert
        AlbumMergeGroup group = Assert.Single(plan);
        Assert.Equal(4, group.SurvivorId);
        Assert.Equal([9L], group.MergedIds);
    }

    [Fact(DisplayName = "album_planner_merges_duplicates_inside_the_target")]
    public void Plan_DuplicatesInsideTheTarget_KeepTheLowestId()
    {
        // Arrange
        AlbumEntity[] target = [Album(30, "Twin"), Album(20, "Twin")];
        AlbumEntity[] source = [Album(1, "twin")];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan(target, source);

        // Assert
        AlbumMergeGroup group = Assert.Single(plan);
        Assert.Equal(20, group.SurvivorId);
        Assert.Equal([1L, 30L], group.MergedIds);
    }

    [Fact(DisplayName = "album_planner_combines_favorite_listens_and_last_listen")]
    public void Plan_Group_CombinesCounters()
    {
        // Arrange
        AlbumEntity[] target = [Album(10, "Album", favorite: false, listens: 3, lastListen: new DateTime(2026, 1, 1))];
        AlbumEntity[] source = [Album(20, "album", favorite: true, listens: 4, lastListen: new DateTime(2026, 2, 2)), Album(21, "ALBUM", listens: 5)];

        // Act
        AlbumMergeGroup group = Assert.Single(AlbumMergePlanner.Plan(target, source));

        // Assert
        Assert.True(group.IsFavorite);
        Assert.Equal(12, group.ListenCount);
        Assert.Equal(new DateTime(2026, 2, 2), group.LastListen);
        Assert.Equal(3, group.ToPreview().AlbumCount);
    }

    [Fact(DisplayName = "album_planner_returns_nothing_without_homonyms")]
    public void Plan_NoHomonyms_ReturnsEmpty()
    {
        // Arrange
        AlbumEntity[] target = [Album(1, "A")];
        AlbumEntity[] source = [Album(2, "B")];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan(target, source);

        // Assert
        Assert.Empty(plan);
    }

    [Fact(DisplayName = "album_planner_groups_a_convertible_compilation_with_its_homonym")]
    public void Plan_ConvertibleCompilation_IsGroupedLikeANormalAlbum()
    {
        // Arrange
        AlbumEntity[] target = [Album(50, "Same Folder")];
        AlbumEntity[] source = [Album(7, "same folder", compilation: true, favorite: true, listens: 3)];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan(target, source, new HashSet<long> { 7 });

        // Assert
        AlbumMergeGroup group = Assert.Single(plan);
        Assert.Equal(50, group.SurvivorId);
        Assert.Equal([7L], group.MergedIds);
        Assert.True(group.IsFavorite);
        Assert.Equal(3, group.ListenCount);
    }

    [Fact(DisplayName = "album_planner_ignores_a_compilation_that_is_not_convertible")]
    public void Plan_NonConvertibleCompilation_IsIgnored()
    {
        // Arrange
        AlbumEntity[] target = [Album(50, "Same Folder")];
        AlbumEntity[] source = [Album(7, "Same Folder", compilation: true)];

        // Act
        IReadOnlyList<AlbumMergeGroup> plan = AlbumMergePlanner.Plan(target, source, new HashSet<long>());

        // Assert
        Assert.Empty(plan);
    }
}