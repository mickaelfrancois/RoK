using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Services;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Common;
using Rok.ViewModels.Genre;
using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre;

public class GenreViewModelTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IStringResourceProvider> _resources = new();
    private readonly Mock<IAlbumViewModelFactory> _albumFactory = new();
    private readonly Mock<IArtistPicture> _artistPicture = new();
    private readonly Mock<IBackdropLoader> _backdropLoader = new();

    private GenreViewModel BuildViewModel()
    {
        _resources.Setup(r => r.GetString(It.IsAny<string>())).Returns(string.Empty);

        return new GenreViewModel(
            _player.Object,
            _resources.Object,
            new GenreDataLoader(_mediator, _albumFactory.Object, NullLogger<GenreDataLoader>.Instance),
            new ArtistPictureService(_artistPicture.Object, NullLogger<ArtistPictureService>.Instance),
            _backdropLoader.Object,
            new GenreEditService(_mediator),
            NullLogger<GenreViewModel>.Instance);
    }

    private void SetupGenreFound(long id)
    {
        _mediator.Setup<GetGenreByIdRequest, Result<GenreDto>>()
                 .Returns(Result<GenreDto>.Ok(new GenreDto { Id = id, Name = "Jazz" }));
        _mediator.Setup<GetAlbumsByGenreIdRequest, IEnumerable<AlbumDto>>()
                 .Returns(new List<AlbumDto>());
    }

    [Fact(DisplayName = "when_the_genre_view_model_is_new_then_it_is_loading_and_commands_are_disabled")]
    public void NewViewModel_ShouldBeLoading_WithDisabledCommands()
    {
        // Arrange
        // Act
        GenreViewModel sut = BuildViewModel();

        // Assert
        Assert.Equal(DetailLoadState.Loading, sut.LoadState);
        Assert.False(sut.IsNotFound);
        Assert.False(sut.ListenCommand.CanExecute(null));
        Assert.False(sut.GenreFavoriteCommand.CanExecute(null));
    }

    [Fact(DisplayName = "when_the_genre_is_not_found_then_the_state_is_not_found_and_commands_stay_disabled")]
    public async Task LoadDataAsync_ShouldReportNotFound_WhenGenreIsMissing()
    {
        // Arrange
        _mediator.Setup<GetGenreByIdRequest, Result<GenreDto>>()
                 .Returns(Result<GenreDto>.Fail(NotFoundError.ForEntity("Genre", 7L)));
        GenreViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(7);

        // Assert
        Assert.Equal(DetailLoadState.NotFound, sut.LoadState);
        Assert.True(sut.IsNotFound);
        Assert.False(sut.ListenCommand.CanExecute(null));
        Assert.False(sut.GenreFavoriteCommand.CanExecute(null));
    }

    [Fact(DisplayName = "when_the_genre_loads_then_every_command_becomes_enabled")]
    public async Task LoadDataAsync_ShouldEnableCommands_WhenGenreIsFound()
    {
        // Arrange
        SetupGenreFound(7);
        GenreViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(7);

        // Assert
        Assert.Equal(DetailLoadState.Loaded, sut.LoadState);
        Assert.False(sut.IsNotFound);
        Assert.True(sut.ListenCommand.CanExecute(null));
        Assert.True(sut.GenreFavoriteCommand.CanExecute(null));
    }

    [Fact(DisplayName = "when_the_genre_is_not_loaded_then_the_empty_album_state_stays_hidden")]
    public async Task HasNoAlbums_ShouldBeFalse_UntilTheGenreIsLoaded()
    {
        // Arrange
        GenreViewModel sut = BuildViewModel();

        // Act
        bool whileLoading = sut.HasNoAlbums;

        _mediator.Setup<GetGenreByIdRequest, Result<GenreDto>>()
                 .Returns(Result<GenreDto>.Fail(NotFoundError.ForEntity("Genre", 7L)));
        await sut.LoadDataAsync(7);
        bool whenNotFound = sut.HasNoAlbums;

        // Assert
        Assert.False(whileLoading);
        Assert.False(whenNotFound);
    }

    [Fact(DisplayName = "when_the_genre_loads_then_every_command_raises_can_execute_changed")]
    public async Task LoadDataAsync_ShouldRaiseCanExecuteChanged_OnEveryCommand()
    {
        // Arrange
        SetupGenreFound(7);
        GenreViewModel sut = BuildViewModel();

        int listenRaised = 0;
        int favoriteRaised = 0;
        sut.ListenCommand.CanExecuteChanged += (_, _) => listenRaised++;
        sut.GenreFavoriteCommand.CanExecuteChanged += (_, _) => favoriteRaised++;

        // Act
        await sut.LoadDataAsync(7);

        // Assert
        // The predicate alone is not enough: a command missing its NotifyCanExecuteChangedFor
        // attribute would still report CanExecute true, yet its button would stay greyed out.
        Assert.True(listenRaised > 0);
        Assert.True(favoriteRaised > 0);
    }

    [Fact(DisplayName = "when_the_genre_loads_with_no_album_then_the_empty_album_state_shows")]
    public async Task HasNoAlbums_ShouldBeTrue_WhenLoadedWithoutAlbum()
    {
        // Arrange
        SetupGenreFound(7);
        GenreViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(7);

        // Assert
        Assert.True(sut.HasNoAlbums);
    }
}