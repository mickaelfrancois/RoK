using Rok.Application.Features.Albums.Edit;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Albums.Edit;

public class AlbumNameRulesTests
{
    [Theory(DisplayName = "normalize_trims_and_returns_the_name_the_import_stores")]
    [InlineData("  Abbey Road  ", "Abbey Road")]
    [InlineData("Live – 1975", "Live - 1975")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsAndFoldsInput(string? input, string expected)
    {
        // Act
        string result = AlbumNameRules.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "album_key_matches_the_import_key")]
    [InlineData("Abbey Road", "abbey road")]
    [InlineData("Live - 1975", "Live — 1975")]
    [InlineData("Live 1975", "Live 1975")]
    [InlineData("  Abbey Road ", "Abbey Road")]
    [InlineData("Élodie", "Élodie")]
    public void HasSameKey_ImportEquivalentNames_ReturnsTrue(string left, string right)
    {
        // Act
        bool result = AlbumNameRules.HasSameKey(left, right);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "album_key_differs_for_different_names")]
    public void HasSameKey_DifferentNames_ReturnsFalse()
    {
        // Act
        bool result = AlbumNameRules.HasSameKey("Abbey Road", "Let It Be");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "collision_is_detected_on_another_non_compilation_album_of_the_target_artist")]
    public void FindCollision_OtherAlbumSameKey_ReturnsIt()
    {
        // Arrange
        var other = new AlbumEntity { Id = 2, Name = "abbey road", TrackCount = 17 };
        var albums = new[] { new AlbumEntity { Id = 1, Name = "Old" }, other };

        // Act
        AlbumEntity? result = AlbumNameRules.FindCollision(albums, 1, "Abbey Road");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal("abbey road", result.Name);
        Assert.Equal(17, result.TrackCount);
    }

    [Fact(DisplayName = "collision_ignores_a_compilation_with_the_same_name")]
    public void FindCollision_CompilationSameName_ReturnsNull()
    {
        // Arrange
        var albums = new[]
        {
            new AlbumEntity { Id = 1, Name = "Old" },
            new AlbumEntity { Id = 2, Name = "Abbey Road", IsCompilation = true }
        };

        // Act
        AlbumEntity? result = AlbumNameRules.FindCollision(albums, 1, "Abbey Road");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "collision_ignores_the_edited_album_itself")]
    public void FindCollision_SameAlbum_ReturnsNull()
    {
        // Arrange
        var albums = new[] { new AlbumEntity { Id = 1, Name = "Abbey Road" } };

        // Act
        AlbumEntity? result = AlbumNameRules.FindCollision(albums, 1, "ABBEY ROAD");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "collision_is_null_when_no_other_album_has_the_key")]
    public void FindCollision_NoMatch_ReturnsNull()
    {
        // Arrange
        var albums = new[] { new AlbumEntity { Id = 1, Name = "Old" }, new AlbumEntity { Id = 2, Name = "Other" } };

        // Act
        AlbumEntity? result = AlbumNameRules.FindCollision(albums, 1, "Abbey Road");

        // Assert
        Assert.Null(result);
    }
}