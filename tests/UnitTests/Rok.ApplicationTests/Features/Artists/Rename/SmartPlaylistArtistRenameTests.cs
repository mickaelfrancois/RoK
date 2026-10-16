using Rok.Application.Dto;
using Rok.Application.Features.Artists.Rename;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Features.Artists.Rename;

public class SmartPlaylistArtistRenameTests
{
    private static PlaylistFilterDto Filter(SmartPlaylistOperator op, string? value, SmartPlaylistEntity entity = SmartPlaylistEntity.Artists, SmartPlaylistField field = SmartPlaylistField.Name)
    {
        return new PlaylistFilterDto { Entity = entity, Field = field, Operator = op, Value = value };
    }

    private static List<PlaylistGroupDto> Groups(params PlaylistFilterDto[] filters)
    {
        return [new PlaylistGroupDto { Filters = [.. filters] }];
    }

    [Theory(DisplayName = "equality_operators_are_rewritten_when_the_value_is_the_old_name_ignoring_case")]
    [InlineData(SmartPlaylistOperator.Equals, "old name")]
    [InlineData(SmartPlaylistOperator.NotEquals, " OLD NAME ")]
    public void Apply_EqualityOperators_ReplaceValue(SmartPlaylistOperator op, string value)
    {
        // Arrange
        var groups = Groups(Filter(op, value));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "Old Name", "New Name");

        // Assert
        Assert.True(changed);
        Assert.Equal("New Name", groups[0].Filters[0].Value);
    }

    [Theory(DisplayName = "like_operators_replace_every_occurrence_and_keep_the_rest")]
    [InlineData(SmartPlaylistOperator.Contains, "%old%", "%New%")]
    [InlineData(SmartPlaylistOperator.NotContains, "x OLD y old_", "x New y New_")]
    [InlineData(SmartPlaylistOperator.StartsWith, "Old%", "New%")]
    [InlineData(SmartPlaylistOperator.EndsWith, "%oLd", "%New")]
    public void Apply_LikeOperators_ReplaceOccurrences(SmartPlaylistOperator op, string value, string expected)
    {
        // Arrange
        var groups = Groups(Filter(op, value));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "Old", "New");

        // Assert
        Assert.True(changed);
        Assert.Equal(expected, groups[0].Filters[0].Value);
    }

    [Theory(DisplayName = "values_not_containing_the_full_old_name_are_untouched")]
    [InlineData(SmartPlaylistOperator.Equals, "Old Name Extra")]
    [InlineData(SmartPlaylistOperator.Contains, "%Ol%")]
    [InlineData(SmartPlaylistOperator.StartsWith, "Other%")]
    public void Apply_ValueWithoutFullOldName_NotChanged(SmartPlaylistOperator op, string value)
    {
        // Arrange
        var groups = Groups(Filter(op, value));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "Old Name", "New Name");

        // Assert
        Assert.False(changed);
        Assert.Equal(value, groups[0].Filters[0].Value);
    }

    [Fact(DisplayName = "album_genre_country_and_other_fields_are_never_touched")]
    public void Apply_OtherEntitiesAndFields_NotChanged()
    {
        // Arrange
        var groups = Groups(
            Filter(SmartPlaylistOperator.Equals, "Old", SmartPlaylistEntity.Albums),
            Filter(SmartPlaylistOperator.Equals, "Old", SmartPlaylistEntity.Genres),
            Filter(SmartPlaylistOperator.Equals, "Old", SmartPlaylistEntity.Countries),
            Filter(SmartPlaylistOperator.Equals, "Old", SmartPlaylistEntity.Artists, SmartPlaylistField.Code),
            Filter(SmartPlaylistOperator.Equals, "Old", SmartPlaylistEntity.Tracks));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "Old", "New");

        // Assert
        Assert.False(changed);
        Assert.All(groups[0].Filters, f => Assert.Equal("Old", f.Value));
    }

    [Fact(DisplayName = "null_value_and_non_text_operators_are_untouched")]
    public void Apply_NullValueOrNumericOperator_NotChanged()
    {
        // Arrange
        var groups = Groups(
            Filter(SmartPlaylistOperator.Equals, null),
            Filter(SmartPlaylistOperator.Between, "Old"));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "Old", "New");

        // Assert
        Assert.False(changed);
        Assert.Null(groups[0].Filters[0].Value);
        Assert.Equal("Old", groups[0].Filters[1].Value);
    }

    [Fact(DisplayName = "an_empty_old_name_changes_nothing")]
    public void Apply_EmptyOldName_NotChanged()
    {
        // Arrange
        var groups = Groups(Filter(SmartPlaylistOperator.Contains, "abc"));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "", "New");

        // Assert
        Assert.False(changed);
        Assert.Equal("abc", groups[0].Filters[0].Value);
    }

    [Fact(DisplayName = "a_rewrite_to_the_same_value_reports_no_change")]
    public void Apply_SameValue_NotChanged()
    {
        // Arrange
        var groups = Groups(Filter(SmartPlaylistOperator.Equals, "AC/DC"));

        // Act
        bool changed = SmartPlaylistArtistRename.Apply(groups, "AC/DC", "AC/DC");

        // Assert
        Assert.False(changed);
    }
}