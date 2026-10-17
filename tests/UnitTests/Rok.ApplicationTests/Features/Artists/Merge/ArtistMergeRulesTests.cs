using Rok.Application.Features.Artists.Merge;

namespace Rok.ApplicationTests.Features.Artists.Merge;

public class ArtistMergeRulesTests
{
    private static ArtistEntity Artist(long id, string name = "Artist")
    {
        return new ArtistEntity { Id = id, Name = name };
    }

    [Fact(DisplayName = "merge_fills_empty_target_fields_from_the_source")]
    public void Merge_EmptyTargetFields_AreFilledFromTheSource()
    {
        // Arrange
        ArtistEntity target = Artist(1);
        target.Biography = "  ";
        target.FormedYear = 0;
        ArtistEntity source = Artist(2);
        source.MusicBrainzID = "mbid";
        source.WikipediaUrl = "https://wiki";
        source.YoutubeUrl = "https://yt";
        source.AudioDbID = "adb";
        source.Biography = "bio";
        source.Members = "a, b";
        source.SimilarArtists = "x";
        source.FormedYear = 1970;
        source.BornYear = 1950;
        source.DiedYear = 2000;
        source.GenreId = 7;
        source.CountryId = 9;

        // Act
        ArtistMergeResult result = ArtistMergeRules.Merge(target, source, true);

        // Assert
        Assert.Equal(1, result.Merged.Id);
        Assert.Equal("mbid", result.Merged.MusicBrainzID);
        Assert.Equal("https://wiki", result.Merged.WikipediaUrl);
        Assert.Equal("https://yt", result.Merged.YoutubeUrl);
        Assert.Equal("adb", result.Merged.AudioDbID);
        Assert.Equal("bio", result.Merged.Biography);
        Assert.Equal("a, b", result.Merged.Members);
        Assert.Equal("x", result.Merged.SimilarArtists);
        Assert.Equal(1970, result.Merged.FormedYear);
        Assert.Equal(1950, result.Merged.BornYear);
        Assert.Equal(2000, result.Merged.DiedYear);
        Assert.Equal(7, result.Merged.GenreId);
        Assert.Equal(9, result.Merged.CountryId);
        Assert.Empty(result.Conflicts);
    }

    [Fact(DisplayName = "merge_never_overwrites_a_non_empty_target_field_and_lists_the_conflict")]
    public void Merge_NonEmptyTargetFields_AreKeptAndConflictsListed()
    {
        // Arrange
        ArtistEntity target = Artist(1);
        target.Biography = "target bio";
        target.OfficialSiteUrl = "https://same";
        target.FormedYear = 1980;
        target.GenreId = 3;
        target.Disbanded = false;
        ArtistEntity source = Artist(2);
        source.Biography = "source bio";
        source.OfficialSiteUrl = " https://same ";
        source.FormedYear = 1981;
        source.GenreId = 4;
        source.Disbanded = true;

        // Act
        ArtistMergeResult result = ArtistMergeRules.Merge(target, source, true);

        // Assert
        Assert.Equal("target bio", result.Merged.Biography);
        Assert.Equal(1980, result.Merged.FormedYear);
        Assert.Equal(3, result.Merged.GenreId);
        Assert.False(result.Merged.Disbanded);
        Assert.Equal(4, result.Conflicts.Count);

        ArtistMergeFieldConflict bio = Assert.Single(result.Conflicts, c => c.Field == "Biography");
        Assert.Equal("target bio", bio.TargetValue);
        Assert.Equal("source bio", bio.SourceValue);
        Assert.Single(result.Conflicts, c => c.Field == "FormedYear" && c.TargetValue == "1980" && c.SourceValue == "1981");
        Assert.Single(result.Conflicts, c => c.Field == "GenreId");
        Assert.Single(result.Conflicts, c => c.Field == "Disbanded" && c.TargetValue == "False" && c.SourceValue == "True");
        Assert.DoesNotContain(result.Conflicts, c => c.Field == "OfficialSiteUrl");
    }

    [Fact(DisplayName = "merge_ors_favorite_and_lock_sums_listens_and_keeps_the_latest_listen")]
    public void Merge_Counters_AreCombined()
    {
        // Arrange
        ArtistEntity target = Artist(1);
        target.IsFavorite = false;
        target.IsLock = true;
        target.ListenCount = 5;
        target.LastListen = new DateTime(2026, 1, 1);
        ArtistEntity source = Artist(2);
        source.IsFavorite = true;
        source.IsLock = false;
        source.ListenCount = 7;
        source.LastListen = new DateTime(2026, 5, 1);

        // Act
        ArtistEntity merged = ArtistMergeRules.Merge(target, source, true).Merged;

        // Assert
        Assert.True(merged.IsFavorite);
        Assert.True(merged.IsLock);
        Assert.Equal(12, merged.ListenCount);
        Assert.Equal(new DateTime(2026, 5, 1), merged.LastListen);
    }

    [Theory(DisplayName = "merge_ignores_a_null_last_listen")]
    [InlineData(true)]
    [InlineData(false)]
    public void Merge_NullLastListen_IsIgnored(bool targetIsNull)
    {
        // Arrange
        DateTime date = new(2026, 3, 3);
        ArtistEntity target = Artist(1);
        target.LastListen = targetIsNull ? null : date;
        ArtistEntity source = Artist(2);
        source.LastListen = targetIsNull ? date : null;

        // Act
        ArtistEntity merged = ArtistMergeRules.Merge(target, source, true).Merged;

        // Assert
        Assert.Equal(date, merged.LastListen);
    }

    [Fact(DisplayName = "merge_takes_the_source_dominant_color_only_when_the_picture_comes_from_the_source")]
    public void Merge_DominantColor_FollowsThePicture()
    {
        // Arrange
        ArtistEntity target = Artist(1);
        target.PictureDominantColor = 111;
        ArtistEntity source = Artist(2);
        source.PictureDominantColor = 222;

        // Act
        ArtistEntity withTargetPicture = ArtistMergeRules.Merge(target, source, true).Merged;
        ArtistEntity withoutTargetPicture = ArtistMergeRules.Merge(target, source, false).Merged;

        // Assert
        Assert.Equal(111, withTargetPicture.PictureDominantColor);
        Assert.Equal(222, withoutTargetPicture.PictureDominantColor);
    }

    [Fact(DisplayName = "merge_keeps_the_target_dominant_color_when_the_source_has_none")]
    public void Merge_SourceWithoutColor_KeepsTargetColor()
    {
        // Arrange
        ArtistEntity target = Artist(1);
        target.PictureDominantColor = 111;
        ArtistEntity source = Artist(2);

        // Act
        ArtistEntity merged = ArtistMergeRules.Merge(target, source, false).Merged;

        // Assert
        Assert.Equal(111, merged.PictureDominantColor);
    }

    [Fact(DisplayName = "merge_keeps_the_target_identity_and_leaves_the_derived_counters_alone")]
    public void Merge_Identity_AndDerivedCounters_AreUntouched()
    {
        // Arrange
        ArtistEntity target = Artist(1, "Target");
        target.CreatDate = new DateTime(2020, 1, 1);
        target.TrackCount = 10;
        target.YearMini = 1990;
        ArtistEntity source = Artist(2, "Source");
        source.TrackCount = 99;
        source.YearMini = 1900;

        // Act
        ArtistEntity merged = ArtistMergeRules.Merge(target, source, true).Merged;

        // Assert
        Assert.Equal("Target", merged.Name);
        Assert.Equal(new DateTime(2020, 1, 1), merged.CreatDate);
        Assert.Equal(10, merged.TrackCount);
        Assert.Equal(1990, merged.YearMini);
    }
}