using System.Windows.Input;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Commons;
using Rok.Services;
using Rok.ViewModels.Album;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Common;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Album;

public class AlbumViewModelTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IStringResourceProvider> _resources = new();
    private readonly Mock<ITrackViewModelFactory> _trackFactory = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<IBackdropLoader> _backdropLoader = new();
    private readonly Mock<IDominantColorCalculator> _dominantColor = new();
    private readonly Mock<IAlbumApiService> _apiService = new();
    private readonly Mock<IPlaylistMenuService> _playlistMenu = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ILastFmClient> _lastFm = new();
    private readonly IMessenger _messenger = new Messenger();

    private AlbumViewModel BuildViewModel()
    {
        _resources.Setup(r => r.GetString(It.IsAny<string>())).Returns(string.Empty);

        return new AlbumViewModel(
            _backdropLoader.Object,
            _lastFm.Object,
            new NavigationService(Mock.Of<ITelemetryClient>()),
            _player.Object,
            _resources.Object,
            new AlbumDataLoader(_mediator, _trackFactory.Object, NullLogger<AlbumDataLoader>.Instance),
            new TagsProvider(_mediator, _messenger),
            new AlbumPictureService(_albumPicture.Object, NullLogger<AlbumPictureService>.Instance),
            _apiService.Object,
            new AlbumStatisticsService(_mediator),
            _dominantColor.Object,
            new AlbumEditService(_mediator, _dialogService.Object),
            _appOptions.Object,
            _dialogService.Object,
            _playlistMenu.Object,
            _messenger,
            TimeProvider.System,
            NullLogger<AlbumViewModel>.Instance);
    }

    private static Dictionary<string, int> TrackCanExecuteChanged(AlbumViewModel sut)
    {
        Dictionary<string, int> raised = new();

        void Watch(string name, ICommand command)
        {
            raised[name] = 0;
            command.CanExecuteChanged += (_, _) => raised[name]++;
        }

        Watch(nameof(sut.AlbumOpenCommand), sut.AlbumOpenCommand);
        Watch(nameof(sut.ArtistOpenCommand), sut.ArtistOpenCommand);
        Watch(nameof(sut.GenreOpenCommand), sut.GenreOpenCommand);
        Watch(nameof(sut.ListenCommand), sut.ListenCommand);
        Watch(nameof(sut.AlbumFavoriteCommand), sut.AlbumFavoriteCommand);
        Watch(nameof(sut.GetDataFromApiCommand), sut.GetDataFromApiCommand);
        Watch(nameof(sut.EditAlbumCommand), sut.EditAlbumCommand);
        Watch(nameof(sut.SelectPictureCommand), sut.SelectPictureCommand);
        Watch(nameof(sut.OpenUrlCommand), sut.OpenUrlCommand);
        Watch(nameof(sut.OpenBiographyCommand), sut.OpenBiographyCommand);

        return raised;
    }

    private static void AssertEveryCommandIsEnabled(AlbumViewModel sut, bool expected)
    {
        Assert.Equal(expected, sut.AlbumOpenCommand.CanExecute(null));
        Assert.Equal(expected, sut.ArtistOpenCommand.CanExecute(null));
        Assert.Equal(expected, sut.GenreOpenCommand.CanExecute(null));
        Assert.Equal(expected, sut.ListenCommand.CanExecute(null));
        Assert.Equal(expected, sut.AlbumFavoriteCommand.CanExecute(null));
        Assert.Equal(expected, sut.GetDataFromApiCommand.CanExecute(null));
        Assert.Equal(expected, sut.EditAlbumCommand.CanExecute(null));
        Assert.Equal(expected, sut.SelectPictureCommand.CanExecute(null));
        Assert.Equal(expected, sut.OpenUrlCommand.CanExecute(null));
        Assert.Equal(expected, sut.OpenBiographyCommand.CanExecute(null));
    }

    [Fact(DisplayName = "when_the_album_view_model_is_new_then_it_is_loading_and_every_command_is_disabled")]
    public void NewViewModel_ShouldBeLoading_WithEveryCommandDisabled()
    {
        // Arrange
        // Act
        AlbumViewModel sut = BuildViewModel();

        // Assert
        Assert.Equal(DetailLoadState.Loading, sut.LoadState);
        Assert.False(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, false);
    }

    [Fact(DisplayName = "when_a_list_tile_calls_set_data_then_every_command_becomes_enabled")]
    public void SetData_ShouldEnableEveryCommand()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, Name = "Greatest Hits" });

        // Assert
        Assert.Equal(DetailLoadState.Loaded, sut.LoadState);
        Assert.False(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, true);
    }

    [Fact(DisplayName = "when_the_album_is_not_found_then_the_state_is_not_found_and_commands_stay_disabled")]
    public async Task LoadDataAsync_ShouldReportNotFound_WhenAlbumIsMissing()
    {
        // Arrange
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Fail(NotFoundError.ForEntity("Album", 42L)));
        AlbumViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        Assert.Equal(DetailLoadState.NotFound, sut.LoadState);
        Assert.True(sut.IsNotFound);
        AssertEveryCommandIsEnabled(sut, false);
    }

    [Fact(DisplayName = "when_the_album_loads_then_every_command_becomes_enabled")]
    public async Task LoadDataAsync_ShouldEnableEveryCommand_WhenAlbumIsFound()
    {
        // Arrange
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        AlbumViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        Assert.Equal(DetailLoadState.Loaded, sut.LoadState);
        Assert.False(sut.IsNotFound);
        Assert.True(sut.HasNoTracks);
        AssertEveryCommandIsEnabled(sut, true);
    }

    [Fact(DisplayName = "when_the_album_loads_then_every_command_raises_can_execute_changed")]
    public async Task LoadDataAsync_ShouldRaiseCanExecuteChanged_OnEveryCommand()
    {
        // Arrange
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        AlbumViewModel sut = BuildViewModel();
        Dictionary<string, int> raised = TrackCanExecuteChanged(sut);

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        // The predicate alone is not enough: a command missing its NotifyCanExecuteChangedFor
        // attribute would still report CanExecute true, yet its button would stay greyed out.
        Assert.All(raised, entry => Assert.True(entry.Value > 0, entry.Key));
    }

    [Fact(DisplayName = "when_the_album_is_not_loaded_then_the_empty_track_state_stays_hidden")]
    public async Task HasNoTracks_ShouldBeFalse_UntilTheAlbumIsLoaded()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        bool whileLoading = sut.HasNoTracks;

        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Fail(NotFoundError.ForEntity("Album", 42L)));
        await sut.LoadDataAsync(42);
        bool whenNotFound = sut.HasNoTracks;

        // Assert
        Assert.False(whileLoading);
        Assert.False(whenNotFound);
    }

    [Fact(DisplayName = "when_the_album_is_not_loaded_then_listen_can_execute_refuses_a_track")]
    public void ListenCommand_CanExecute_ShouldRefuseATrack_WhenTheAlbumIsNotLoaded()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        bool canListen = sut.ListenCommand.CanExecute(null);

        // Assert
        // AlbumPage.OnTrackTitleClick consults this guard before calling Execute, because
        // ICommand.Execute never checks CanExecute on its own.
        Assert.False(canListen);
    }
}