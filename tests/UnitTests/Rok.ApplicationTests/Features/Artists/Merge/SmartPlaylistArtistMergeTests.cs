using Rok.Application.Dto;
using Rok.Application.Features.Artists.Merge;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Features.Artists.Merge;

public class SmartPlaylistArtistMergeTests
{
    private const string Source = "Beatles";
    private const string Target = "The Beatles";

    private static PlaylistFilterDto Filter(SmartPlaylistOperator op, string? value, SmartPlaylistEntity entity = SmartPlaylistEntity.Artists, SmartPlaylistField field = SmartPlaylistField.Name)
    {
        return new PlaylistFilterDto { Entity = entity, Field = field, Operator = op, Value = value };
    }

    private static List<PlaylistGroupDto> Groups(params PlaylistFilterDto[] filters)
    {
        return [new PlaylistGroupDto { Filters = [.. filters] }];
    }

    [Theory(DisplayName = "smart_playlist_impact_lists_filters_on_source_or_target")]
    [InlineData(SmartPlaylistOperator.Equals, "beatles", true)]
    [InlineData(SmartPlaylistOperator.NotEquals, " The Beatles ", false)]
    [InlineData(SmartPlaylistOperator.Contains, "%Beat%", false)]
    [InlineData(SmartPlaylistOperator.NotContains, "Beatles", true)]
    [InlineData(SmartPlaylistOperator.StartsWith, "The B%", false)]
    [InlineData(SmartPlaylistOperator.EndsWith, "%tles", false)]
    public void Describe_ArtistNameFilter_IsListed(SmartPlaylistOperator op, string value, bool rewritten)
    {
        // Arrange
        var groups = Groups(Filter(op, value));

        // Act
        SmartPlaylistMergeImpact? impact = SmartPlaylistArtistMerge.Describe(5, "Mix", groups, Source, Target);

        // Assert
        Assert.NotNull(impact);
        Assert.Equal(5, impact.PlaylistId);
        Assert.Equal("Mix", impact.Name);
        Assert.Equal(rewritten, impact.IsRewritten);
    }

    [Fact(DisplayName = "smart_playlist_impact_ignores_album_genre_and_unrelated_artist_filters")]
    public void Describe_OtherFilters_AreNotListed()
    {
        // Arrange
        var groups = Groups(
            Filter(SmartPlaylistOperator.Equals, Source, SmartPlaylistEntity.Albums),
            Filter(SmartPlaylistOperator.Equals, Source, SmartPlaylistEntity.Genres),
            Filter(SmartPlaylistOperator.Equals, Source, SmartPlaylistEntity.Artists, SmartPlaylistField.IsFavorite),
            Filter(SmartPlaylistOperator.Equals, "Queen"),
            Filter(SmartPlaylistOperator.GreaterThan, Source),
            Filter(SmartPlaylistOperator.Contains, "%"),
            Filter(SmartPlaylistOperator.Equals, null));

        // Act
        SmartPlaylistMergeImpact? impact = SmartPlaylistArtistMerge.Describe(5, "Mix", groups, Source, Target);

        // Assert
        Assert.Null(impact);
    }

    [Fact(DisplayName = "smart_playlist_describe_does_not_modify_the_criteria")]
    public void Describe_DoesNotModifyTheGroups()
    {
        // Arrange
        var groups = Groups(Filter(SmartPlaylistOperator.Equals, Source));

        // Act
        SmartPlaylistArtistMerge.Describe(5, "Mix", groups, Source, Target);

        // Assert
        Assert.Equal(Source, groups[0].Filters[0].Value);
    }

    [Fact(DisplayName = "smart_playlist_apply_rewrites_the_source_name_to_the_target_name")]
    public void Apply_RewritesTheSourceName()
    {
        // Arrange
        var groups = Groups(
            Filter(SmartPlaylistOperator.Equals, Source),
            Filter(SmartPlaylistOperator.NotEquals, Source),
            Filter(SmartPlaylistOperator.Contains, "%Beatles%"),
            Filter(SmartPlaylistOperator.Equals, "Queen"));

        // Act
        bool changed = SmartPlaylistArtistMerge.Apply(groups, Source, Target);

        // Assert
        Assert.True(changed);
        Assert.Equal(Target, groups[0].Filters[0].Value);
        Assert.Equal(Target, groups[0].Filters[1].Value);
        Assert.Equal("%The Beatles%", groups[0].Filters[2].Value);
        Assert.Equal("Queen", groups[0].Filters[3].Value);
    }

    [Fact(DisplayName = "smart_playlist_that_only_names_the_target_is_listed_but_not_rewritten")]
    public void Describe_OnlyTarget_IsNotRewritten()
    {
        // Arrange
        var groups = Groups(Filter(SmartPlaylistOperator.Equals, Target));

        // Act
        SmartPlaylistMergeImpact? impact = SmartPlaylistArtistMerge.Describe(8, "Fans", groups, Source, Target);

        // Assert
        Assert.NotNull(impact);
        Assert.False(impact.IsRewritten);
    }
}