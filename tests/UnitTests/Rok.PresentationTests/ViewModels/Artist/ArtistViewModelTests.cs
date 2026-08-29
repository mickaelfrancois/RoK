using System.Windows.Input;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Tags.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Commons;
using Rok.Infrastructure.Files;
using Rok.Services;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Common;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Artist;

public class ArtistViewModelTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IStringResourceProvider> _resources = new();
    private readonly Mock<IAlbumViewModelFactory> _albumFactory = new();
    private readonly Mock<ITrackViewModelFactory> _trackFactory = new();
    private readonly Mock<IArtistPicture> _artistPicture = new();
    private readonly Mock<IBackdropLoader> _backdropLoader = new();
    private readonly Mock<IDominantColorCalculator> _dominantColor = new();
    private readonly Mock<IArtistApiService> _apiService = new();
    private readonly Mock<IPlaylistMenuService> _playlistMenu = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly IMessenger _messenger = new Messenger();

    private ArtistViewModel BuildViewModel()
    {
        _resources.Setup(r => r.GetString(It.IsAny<string>())).Returns(string.Empty);
        _appOptions.SetupGet(o => o.CachePath).Returns(Path.GetTempPath());

        return new ArtistViewModel(
            _backdropLoader.Object,
            new NavigationService(Mock.Of<ITelemetryClient>()),
            _player.Object,
            _dialogService.Object,
            _resources.Object,
            new ArtistDataLoader(_mediator, _albumFactory.Object, _trackFactory.Object, NullLogger<ArtistDataLoader>.Instance),
            new TagsProvider(_mediator, _messenger),
            new ArtistPictureService(_artistPicture.Object, NullLogger<ArtistPictureService>.Instance),
            _apiService.Object,
            new ArtistStatisticsService(_mediator),
            _dominantColor.Object,
            new ArtistEditService(_mediator, _dialogService.Object, NullLogger<ArtistEditService>.Instance),
            new BackdropPicture(_appOptions.Object, NullLogger<BackdropPicture>.Instance),
            _appOptions.Object,
            _playlistMenu.Object,
            _messenger,
            NullLogger<ArtistViewModel>.Instance);
    }

    private static Dictionary<string, int> TrackCanExecuteChanged(ArtistViewModel sut)
    {
        Dictionary<string, int> raised = new();

        void Watch(string name, ICommand command)
        {
            raised[name] = 0;
            command.CanExecuteChanged += (_, _) => raised[name]++;
        }

        Watch(nameof(sut.ArtistOpenCommand), sut.ArtistOpenCommand);
        Watch(nameof(sut.GenreOpenCommand), sut.GenreOpenCommand);
        Watch(nameof(sut.ListenCommand), sut.ListenCommand);
        Watch(nameof(sut.ArtistFavoriteCommand), sut.ArtistFavoriteCommand);
        Watch(nameof(sut.OpenOfficialSiteCommand), sut.OpenOfficialSiteCommand);
        Watch(nameof(sut.EditArtistCommand), sut.EditArtistCommand);
        Watch(nameof(sut.SelectPictureCommand), sut.SelectPictureCommand);
        Watch(nameof(sut.OpenUrlCommand), sut.OpenUrlCommand);
        Watch(nameof(sut.OpenBiographyCommand), sut.OpenBiographyCommand);

        return raised;
    }

    private static void AssertEveryCommandIsEnabled(ArtistViewModel sut, bool expected)
    {
        Assert.Equal(expected, sut.ArtistOpenCommand.CanExecute(null));
        Assert.Equal(expected, sut.GenreOpenCommand.CanExecute(null));
        Assert.Equal(expected, sut.ListenCommand.CanExecute(null));
        Assert.Equal(expected, sut.ArtistFavoriteCommand.CanExecute(null));
        Assert.Equal(expected, sut.OpenOfficialSiteCommand.CanExecute(null));
        Assert.Equal(expected, sut.EditArtistCommand.CanExecute(null));
        Assert.Equal(expected, sut.SelectPictureCommand.CanExecute(null));
        Assert.Equal(expected, sut.OpenUrlCommand.CanExecute(null));
        Assert.Equal(expected, sut.OpenBiographyCommand.CanExecute(null));
    }

    [Fact(DisplayName = "when_the_artist_view_model_is_new_then_it_is_loading_and_every_command_is_disabled")]
    public void NewViewModel_ShouldBeLoading_WithEveryCommandDisabled()
    {
        // Arrange
        // Act
        ArtistViewModel sut = BuildViewModel();

        // Assert
        Assert.Equal(DetailLoadState.Loading, sut.LoadState);
        Assert.False(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, false);
    }

    [Fact(DisplayName = "when_a_list_tile_calls_set_data_then_every_command_becomes_enabled")]
    public void SetData_ShouldEnableEveryCommand()
    {
        // Arrange
        ArtistViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new ArtistDto { Id = 7, Name = "Artist X" });

        // Assert
        Assert.Equal(DetailLoadState.Loaded, sut.LoadState);
        Assert.False(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, true);
    }

    [Fact(DisplayName = "when_the_artist_is_not_found_then_the_state_is_not_found_and_commands_stay_disabled")]
    public async Task LoadDataAsync_ShouldReportNotFound_WhenArtistIsMissing()
    {
        // Arrange
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>()
                 .Returns(Result<ArtistDto>.Fail(NotFoundError.ForEntity("Artist", 7L)));
        ArtistViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(7);

        // Assert
        Assert.Equal(DetailLoadState.NotFound, sut.LoadState);
        Assert.True(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, false);
    }

    [Fact(DisplayName = "when_the_artist_loads_then_every_command_becomes_enabled")]
    public async Task LoadDataAsync_ShouldEnableEveryCommand_WhenArtistIsFound()
    {
        // Arrange
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>()
                 .Returns(Result<ArtistDto>.Ok(new ArtistDto { Id = 7, Name = "Artist X" }));
        _mediator.Setup<GetAlbumsByArtistIdRequest, IEnumerable<AlbumDto>>()
                 .Returns(new List<AlbumDto>());
        _mediator.Setup<GetTracksByArtistIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        _mediator.Setup<GetAllTagsRequest, IEnumerable<TagDto>>()
                 .Returns(new List<TagDto>());
        ArtistViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(7, loadAlbums: true, loadTracks: true, fetchApi: false);

        // Assert
        Assert.Equal(DetailLoadState.Loaded, sut.LoadState);
        Assert.False(sut.IsNotFound);
        Assert.True(sut.HasNoAlbums);
        Assert.True(sut.HasNoTracks);
        AssertEveryCommandIsEnabled(sut, true);
    }

    [Fact(DisplayName = "when_the_artist_loads_then_every_command_raises_can_execute_changed")]
    public async Task LoadDataAsync_ShouldRaiseCanExecuteChanged_OnEveryCommand()
    {
        // Arrange
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>()
                 .Returns(Result<ArtistDto>.Ok(new ArtistDto { Id = 7, Name = "Artist X" }));
        _mediator.Setup<GetAlbumsByArtistIdRequest, IEnumerable<AlbumDto>>()
                 .Returns(new List<AlbumDto>());
        _mediator.Setup<GetTracksByArtistIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        _mediator.Setup<GetAllTagsRequest, IEnumerable<TagDto>>()
                 .Returns(new List<TagDto>());
        ArtistViewModel sut = BuildViewModel();
        Dictionary<string, int> raised = TrackCanExecuteChanged(sut);

        // Act
        await sut.LoadDataAsync(7, loadAlbums: true, loadTracks: true, fetchApi: false);

        // Assert
        // The predicate alone is not enough: a command missing its NotifyCanExecuteChangedFor
        // attribute would still report CanExecute true, yet its button would stay greyed out.
        Assert.All(raised, entry => Assert.True(entry.Value > 0, entry.Key));
    }

    [Fact(DisplayName = "when_the_artist_is_not_loaded_then_the_empty_states_stay_hidden")]
    public async Task EmptyStates_ShouldBeFalse_UntilTheArtistIsLoaded()
    {
        // Arrange
        ArtistViewModel sut = BuildViewModel();

        // Act
        bool albumsWhileLoading = sut.HasNoAlbums;
        bool tracksWhileLoading = sut.HasNoTracks;

        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>()
                 .Returns(Result<ArtistDto>.Fail(NotFoundError.ForEntity("Artist", 7L)));
        await sut.LoadDataAsync(7);

        // Assert
        Assert.False(albumsWhileLoading);
        Assert.False(tracksWhileLoading);
        Assert.False(sut.HasNoAlbums);
        Assert.False(sut.HasNoTracks);
    }
}