using Rok.Application.Dto;
using Rok.Application.Features.Artists.Rename;

namespace Rok.ApplicationTests.Features.Artists.Rename;

public class ArtistNameRulesTests
{
    [Theory(DisplayName = "normalize_trims_and_returns_the_name_the_import_stores")]
    [InlineData("  AC/DC  ", "AC/DC")]
    [InlineData("AC/DC", "AC/DC")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsInput(string? input, string expected)
    {
        // Act
        string result = ArtistNameRules.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "find_collision_matches_another_artist_with_a_case_variant")]
    public void FindCollision_CaseVariant_ReturnsOther()
    {
        // Arrange
        var other = new ArtistDto { Id = 2, Name = "Élodie", AlbumCount = 1, TrackCount = 4 };
        var artists = new[] { new ArtistDto { Id = 1, Name = "Old" }, other };

        // Act
        ArtistDto? result = ArtistNameRules.FindCollision(artists, 1, "élodie");

        // Assert
        Assert.Same(other, result);
    }

    [Fact(DisplayName = "find_collision_matches_a_pending_rename_marker")]
    public void FindCollision_PendingMarker_ReturnsOwner()
    {
        // Arrange
        var other = new ArtistDto { Id = 2, Name = "Newer", RenamePendingFrom = "Target" };
        var artists = new[] { new ArtistDto { Id = 1, Name = "Old" }, other };

        // Act
        ArtistDto? result = ArtistNameRules.FindCollision(artists, 1, "TARGET");

        // Assert
        Assert.Same(other, result);
    }

    [Fact(DisplayName = "find_collision_ignores_a_case_only_rename_of_the_same_artist")]
    public void FindCollision_SameArtistCaseOnly_ReturnsNull()
    {
        // Arrange
        var artists = new[] { new ArtistDto { Id = 1, Name = "Ac/dc" }, new ArtistDto { Id = 2, Name = "Other" } };

        // Act
        ArtistDto? result = ArtistNameRules.FindCollision(artists, 1, "AC/DC");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "find_collision_returns_null_when_no_key_matches")]
    public void FindCollision_NoMatch_ReturnsNull()
    {
        // Arrange
        var artists = new[] { new ArtistDto { Id = 2, Name = "Other", RenamePendingFrom = "Another" } };

        // Act
        ArtistDto? result = ArtistNameRules.FindCollision(artists, 1, "Brand New");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "has_same_key_is_true_for_case_only_difference")]
    public void HasSameKey_CaseOnly_ReturnsTrue()
    {
        // Act
        bool result = ArtistNameRules.HasSameKey("Ac/dc", "AC/DC");

        // Assert
        Assert.True(result);
    }
}