using Rok.Application.Dto;
using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreAlbumSorterTests
{
    private static readonly List<AlbumDto> Albums =
    [
        new() { Name = "Beta", ArtistName = "Zed", Year = 2001, ListenCount = 3, CreatDate = new DateTime(2024, 1, 1) },
        new() { Name = "alpha", ArtistName = "Alice", Year = 1999, ListenCount = 9, CreatDate = new DateTime(2025, 1, 1) },
        new() { Name = "Gamma", ArtistName = "Alice", ReleaseDate = new DateTime(1995, 5, 1), Year = 1990, ListenCount = 3, CreatDate = new DateTime(2023, 1, 1) },
        new() { Name = "Delta", ArtistName = "Zed", ListenCount = 0, CreatDate = new DateTime(2022, 1, 1) },
    ];

    private static List<string> Names(GenreAlbumSort sort)
    {
        return GenreAlbumSorter.Sort(Albums, album => album, sort).Select(album => album.Name).ToList();
    }

    [Fact(DisplayName = "when_sorting_by_name_then_albums_are_ordered_alphabetically_ignoring_case")]
    public void Sort_ByName_ShouldIgnoreCase()
    {
        Assert.Equal(["alpha", "Beta", "Delta", "Gamma"], Names(GenreAlbumSort.Name));
    }

    [Fact(DisplayName = "when_sorting_by_artist_then_albums_follow_the_artist_then_the_year")]
    public void Sort_ByArtist_ShouldOrderByArtistThenYear()
    {
        Assert.Equal(["Gamma", "alpha", "Delta", "Beta"], Names(GenreAlbumSort.Artist));
    }

    [Fact(DisplayName = "when_sorting_by_year_then_the_newest_come_first_and_unknown_years_last")]
    public void Sort_ByYear_ShouldPutTheNewestFirstAndUnknownLast()
    {
        Assert.Equal(["Beta", "alpha", "Gamma", "Delta"], Names(GenreAlbumSort.Year));
    }

    [Fact(DisplayName = "when_sorting_by_date_added_then_the_most_recent_come_first")]
    public void Sort_ByDateAdded_ShouldPutTheMostRecentFirst()
    {
        Assert.Equal(["alpha", "Beta", "Gamma", "Delta"], Names(GenreAlbumSort.DateAdded));
    }

    [Fact(DisplayName = "when_sorting_by_listens_then_the_most_listened_come_first_and_ties_follow_the_name")]
    public void Sort_ByMostListened_ShouldBreakTiesByName()
    {
        Assert.Equal(["alpha", "Beta", "Gamma", "Delta"], Names(GenreAlbumSort.MostListened));
    }

    [Fact(DisplayName = "when_the_sort_is_unknown_then_sorting_is_refused")]
    public void Sort_ShouldThrow_WhenTheSortIsUnknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GenreAlbumSorter.Sort(Albums, album => album, (GenreAlbumSort)99));
    }

    [Fact(DisplayName = "when_sorting_then_the_source_list_is_left_untouched")]
    public void Sort_ShouldNotMutateTheSource()
    {
        // Arrange
        List<AlbumDto> source = [.. Albums];

        // Act
        GenreAlbumSorter.Sort(source, album => album, GenreAlbumSort.Name);

        // Assert
        Assert.Equal(Albums, source);
    }
}