using Rok.Application.Dto;
using Rok.ViewModels.Common;
using Rok.ViewModels.Common.Services;

namespace Rok.PresentationTests.ViewModels.Common.Services;

public class FilterChipBuilderTests
{
    private static readonly List<GenreDto> Genres = [new() { Id = 2, Name = "Rock" }, new() { Id = 5, Name = "Jazz" }];

    private static string Label(string filter) => "label " + filter;

    [Fact(DisplayName = "build_should_return_no_chip_without_filter")]
    public void Build_ShouldReturnNoChip_WithoutFilter()
    {
        // Arrange / Act
        List<FilterChip> chips = FilterChipBuilder.Build([], [], [], Genres, Label);

        // Assert
        Assert.Empty(chips);
    }

    [Fact(DisplayName = "build_should_return_one_chip_per_filter_across_families")]
    public void Build_ShouldReturnOneChipPerFilter_AcrossFamilies()
    {
        // Arrange / Act
        List<FilterChip> chips = FilterChipBuilder.Build(["LIVE"], [2], ["80s"], Genres, Label);

        // Assert
        Assert.Collection(chips,
            chip => Assert.Equal(new FilterChip(FilterKind.List, "LIVE", "label LIVE"), chip),
            chip => Assert.Equal(new FilterChip(FilterKind.Genre, "2", "Rock"), chip),
            chip => Assert.Equal(new FilterChip(FilterKind.Tag, "80s", "80s"), chip));
    }

    [Fact(DisplayName = "build_should_keep_selection_order_inside_a_family")]
    public void Build_ShouldKeepSelectionOrder_InsideAFamily()
    {
        // Arrange / Act
        List<FilterChip> chips = FilterChipBuilder.Build([], [5, 2], [], Genres, Label);

        // Assert
        Assert.Equal(["Jazz", "Rock"], chips.Select(c => c.Label));
    }

    [Fact(DisplayName = "build_should_skip_a_genre_that_no_longer_exists")]
    public void Build_ShouldSkipUnknownGenre()
    {
        // Arrange / Act
        List<FilterChip> chips = FilterChipBuilder.Build([], [99], [], Genres, Label);

        // Assert
        Assert.Empty(chips);
    }
}