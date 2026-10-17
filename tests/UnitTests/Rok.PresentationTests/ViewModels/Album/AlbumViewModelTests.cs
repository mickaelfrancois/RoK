using System.Windows.Input;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Commons;
using Rok.Services;
using Rok.Shared.Enums;
using Rok.ViewModels.Album;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Common;
using Rok.ViewModels.Tracks.Interfaces;
using AlbumTagWriteOutcome = Rok.Application.Features.Albums.Edit.AlbumTagWriteOutcome;

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

    private sealed class RecordingNavigationService() : NavigationService(Mock.Of<ITelemetryClient>())
    {
        public List<long> NavigatedAlbums { get; } = [];

        public override void NavigateToAlbum(long albumId) => NavigatedAlbums.Add(albumId);
    }

    private AlbumViewModel BuildViewModel(NavigationService? navigationService = null)
    {
        _resources.Setup(r => r.GetString(It.IsAny<string>())).Returns(string.Empty);

        return new AlbumViewModel(
            _backdropLoader.Object,
            _lastFm.Object,
            navigationService ?? new NavigationService(Mock.Of<ITelemetryClient>()),
            _player.Object,
            _resources.Object,
            new AlbumDataLoader(_mediator, _trackFactory.Object, NullLogger<AlbumDataLoader>.Instance),
            new AlbumPictureService(_albumPicture.Object, NullLogger<AlbumPictureService>.Instance),
            _apiService.Object,
            new AlbumStatisticsService(_mediator),
            _dominantColor.Object,
            new AlbumEditService(_mediator, _dialogService.Object, _appOptions.Object, _resources.Object, NullLogger<AlbumEditService>.Instance),
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
        Watch(nameof(sut.ShuffleCommand), sut.ShuffleCommand);
        Watch(nameof(sut.AlbumFavoriteCommand), sut.AlbumFavoriteCommand);
        Watch(nameof(sut.GetDataFromApiCommand), sut.GetDataFromApiCommand);
        Watch(nameof(sut.EditAlbumCommand), sut.EditAlbumCommand);
        Watch(nameof(sut.ResumeTagWriteCommand), sut.ResumeTagWriteCommand);
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
        Assert.Equal(expected, sut.ShuffleCommand.CanExecute(null));
        Assert.Equal(expected, sut.AlbumFavoriteCommand.CanExecute(null));
        Assert.Equal(expected, sut.GetDataFromApiCommand.CanExecute(null));
        Assert.Equal(expected, sut.EditAlbumCommand.CanExecute(null));
        Assert.Equal(expected, sut.ResumeTagWriteCommand.CanExecute(null));
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

    [Fact(DisplayName = "when_the_album_loads_then_the_album_and_its_computed_properties_are_notified")]
    public async Task LoadDataAsync_ShouldNotifyAlbumAndComputedProperties()
    {
        // Arrange
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits", TrackCount = 11, Year = 1977 }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        AlbumViewModel sut = BuildViewModel();
        List<string?> raised = [];
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        Assert.Contains(nameof(sut.Album), raised);
        Assert.Contains(nameof(sut.SubTitle), raised);
        Assert.Contains(nameof(sut.ReleaseDateYear), raised);
        Assert.Contains(nameof(sut.DurationTotal), raised);
        Assert.Contains(nameof(sut.Tags), raised);
    }

    [Fact(DisplayName = "when_a_list_tile_calls_set_data_then_the_album_is_notified")]
    public void SetData_ShouldNotifyAlbum()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();
        List<string?> raised = [];
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        sut.SetData(new AlbumDto { Id = 42, Name = "Greatest Hits" });

        // Assert
        Assert.Contains(nameof(sut.Album), raised);
    }

    [Fact(DisplayName = "when_the_api_refreshes_the_album_then_the_album_and_the_subtitle_are_notified")]
    public async Task GetDataFromApi_ShouldNotifyAlbumAndSubTitle_WhenDataIsUpdated()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();
        sut.SetData(new AlbumDto { Id = 42, Name = "Greatest Hits" });

        _apiService.Setup(a => a.GetAndUpdateAlbumDataAsync(It.IsAny<AlbumDto>(), It.IsAny<IAlbumPictureService>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new AlbumApiUpdateResult(DataUpdated: true, PictureDownloaded: false));
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits", TrackCount = 11 }));

        List<string?> raised = [];
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        await sut.GetDataFromApiCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(nameof(sut.Album), raised);
        Assert.Contains(nameof(sut.SubTitle), raised);
        Assert.Equal(11, sut.Album.TrackCount);
    }

    [Fact(DisplayName = "when_the_album_has_tracks_and_a_duration_then_the_header_summary_joins_them")]
    public void HeaderSummary_ShouldJoinTrackCountAndDuration()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();
        _resources.Setup(r => r.GetString("listeningSummaryTracks")).Returns("{0} tracks");
        _resources.Setup(r => r.GetString("listeningSummaryMinutes")).Returns("{0} min");

        // Act
        sut.SetData(new AlbumDto { Id = 42, TrackCount = 11, Duration = 2340 });

        // Assert
        Assert.Equal("11 tracks · 39 min", sut.HeaderSummary);
    }

    [Fact(DisplayName = "when_the_album_has_no_tracks_then_the_header_summary_is_empty")]
    public void HeaderSummary_ShouldBeEmpty_WhenTheAlbumHasNoTracks()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, TrackCount = 0, Duration = 0 });

        // Assert
        Assert.Equal(string.Empty, sut.HeaderSummary);
    }

    [Theory(DisplayName = "when_the_album_has_a_release_date_or_a_year_then_the_year_text_prefers_the_release_date")]
    [InlineData(1977, 1990, "1977")]
    [InlineData(null, 1990, "1990")]
    [InlineData(null, null, "")]
    public void YearText_ShouldPreferTheReleaseDate(int? releaseYear, int? year, string expected)
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();
        DateTime? releaseDate = releaseYear.HasValue ? new DateTime(releaseYear.Value, 2, 4) : null;

        // Act
        sut.SetData(new AlbumDto { Id = 42, ReleaseDate = releaseDate, Year = year });

        // Assert
        Assert.Equal(expected, sut.YearText);
        Assert.Equal(expected.Length > 0, sut.HasYear);
    }

    [Theory(DisplayName = "when_the_album_has_a_genre_id_and_a_name_then_the_genre_chip_is_shown")]
    [InlineData(5L, "Rock", true)]
    [InlineData(5L, "", false)]
    [InlineData(null, "Rock", false)]
    public void HasGenre_ShouldRequireAnIdAndAName(long? genreId, string genreName, bool expected)
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, GenreId = genreId, GenreName = genreName });

        // Assert
        Assert.Equal(expected, sut.HasGenre);
    }

    [Fact(DisplayName = "when_the_album_is_a_favorite_then_the_favorite_label_offers_to_remove_it")]
    public void AlbumFavoriteLabel_ShouldOfferToRemove_WhenTheAlbumIsAFavorite()
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();
        _resources.Setup(r => r.GetString("listeningFavoriteAdd")).Returns("Add {0}");
        _resources.Setup(r => r.GetString("listeningFavoriteRemove")).Returns("Remove {0}");

        // Act
        sut.SetData(new AlbumDto { Id = 42, Name = "Rumours", ArtistName = "Fleetwood Mac", IsFavorite = true, IsArtistFavorite = false });

        // Assert
        Assert.Equal("Remove Rumours", sut.AlbumFavoriteLabel);
    }

    [Fact(DisplayName = "when_the_album_has_no_tracks_then_shuffle_does_not_load_a_playlist")]
    public async Task ShuffleCommand_ShouldNotLoadAPlaylist_WhenTheAlbumHasNoTracks()
    {
        // Arrange
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        AlbumViewModel sut = BuildViewModel();
        await sut.LoadDataAsync(42);

        // Act
        await sut.ShuffleCommand.ExecuteAsync(null);

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto?>()), Times.Never);
    }

    [Theory(DisplayName = "when_the_album_has_a_label_or_a_format_then_the_header_details_join_them")]
    [InlineData("Warner Bros.", "Vinyl", "Warner Bros. · Vinyl", true)]
    [InlineData("Warner Bros.", null, "Warner Bros.", true)]
    [InlineData(null, " CD ", "CD", true)]
    [InlineData("  ", "", "", false)]
    public void HeaderDetails_ShouldJoinTheLabelAndTheFormat(string? label, string? format, string expected, bool hasDetails)
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, Label = label, ReleaseFormat = format });

        // Assert
        Assert.Equal(expected, sut.HeaderDetails);
        Assert.Equal(hasDetails, sut.HasDetails);
    }

    [Theory(DisplayName = "a_completed_or_partial_resume_reloads_the_album_and_sends_the_messages")]
    [InlineData(EArtistRenameState.Completed, false)]
    [InlineData(EArtistRenameState.Partial, true)]
    public async Task ResumeTagWrite_AfterAnOutcome_ShouldReloadTheAlbumAndSendTheMessages(EArtistRenameState state, bool stillPending)
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _dialogService
            .Setup(d => d.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .ReturnsAsync(new AlbumTagWriteOutcome(state, 42, [], [], [3, 4], [90]));
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Renamed" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        List<(string Kind, long Id, ActionType Action)> sent = [];
        _messenger.Subscribe<AlbumUpdateMessage>(m => sent.Add(("album", m.Id, m.Action)));
        _messenger.Subscribe<ArtistUpdateMessage>(m => sent.Add(("artist", m.Id, m.Action)));
        AlbumViewModel sut = BuildViewModel();
        sut.SetData(new AlbumDto { Id = 42, Name = "Old" });

        // Act
        await sut.ResumeTagWriteCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Renamed", sut.Album.Name);
        Assert.Equal(stillPending, sut.HasPendingTagWrite);
        Assert.Equal(stillPending, sut.ShowPendingTagWrite);
        Assert.Contains(("album", 42L, ActionType.Update), sent);
        Assert.Contains(("album", 90L, ActionType.Delete), sent);
        Assert.Contains(("artist", 3L, ActionType.Update), sent);
        Assert.Contains(("artist", 4L, ActionType.Update), sent);
    }

    [Fact(DisplayName = "a_resume_that_was_blocked_or_dismissed_changes_nothing_on_the_page")]
    public async Task ResumeTagWrite_WhenNothingWasWritten_ShouldSendNoMessage()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _dialogService
            .Setup(d => d.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .ReturnsAsync(new AlbumTagWriteOutcome(EArtistRenameState.Blocked, 42, [], [], [], []));
        int messages = 0;
        _messenger.Subscribe<AlbumUpdateMessage>(_ => messages++);
        AlbumViewModel sut = BuildViewModel();
        sut.SetData(new AlbumDto { Id = 42, Name = "Old" });

        // Act
        await sut.ResumeTagWriteCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, messages);
        Assert.Empty(_mediator.Sent<GetAlbumByIdRequest>());
    }

    [Fact(DisplayName = "the_pending_bar_shows_only_when_a_write_is_pending_and_writing_tags_is_on")]
    public async Task ShowPendingTagWrite_FollowsThePendingStateAndTheOption()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        _mediator.Setup<GetAlbumPendingTagWriteRequest, Result<bool>>().Returns(Result<bool>.Ok(true));
        AlbumViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        Assert.True(sut.HasPendingTagWrite);
        Assert.True(sut.ShowPendingTagWrite);

        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(false);
        sut.SetData(new AlbumDto { Id = 42, Name = "Greatest Hits" });

        Assert.False(sut.ShowPendingTagWrite);
    }

    [Theory(DisplayName = "merge_is_hidden_when_writing_tags_is_off_or_the_album_is_a_compilation")]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void SetData_ShouldAllowTheMerge_OnlyWithTheOptionOnAndNoCompilation(bool writeTagsEnabled, bool isCompilation, bool expected)
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(writeTagsEnabled);
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, Name = "Greatest Hits", IsCompilation = isCompilation });

        // Assert
        Assert.Equal(expected, sut.CanMerge);
        Assert.Equal(expected, sut.MergeWithCommand.CanExecute(null));
    }

    [Fact(DisplayName = "merge_is_hidden_while_a_tag_write_is_pending")]
    public async Task LoadDataAsync_WithAPendingWrite_ShouldNotAllowTheMerge()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Greatest Hits" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        _mediator.Setup<GetAlbumPendingTagWriteRequest, Result<bool>>().Returns(Result<bool>.Ok(true));
        AlbumViewModel sut = BuildViewModel();

        // Act
        await sut.LoadDataAsync(42);

        // Assert
        Assert.True(sut.HasPendingTagWrite);
        Assert.False(sut.CanMerge);
        Assert.False(sut.MergeWithCommand.CanExecute(null));
    }

    [Fact(DisplayName = "the_merge_command_is_disabled_until_the_album_is_loaded")]
    public void NewViewModel_ShouldNotAllowTheMerge_WhileLoading()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);

        // Act
        AlbumViewModel sut = BuildViewModel();

        // Assert
        Assert.False(sut.MergeWithCommand.CanExecute(null));
    }

    [Fact(DisplayName = "merging_the_displayed_album_away_navigates_to_the_target_and_sends_the_messages")]
    public async Task MergeWith_WhenTheDisplayedAlbumDisappears_ShouldNavigateToTheTarget()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _dialogService
            .Setup(d => d.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()))
            .ReturnsAsync(new AlbumTagWriteOutcome(EArtistRenameState.Completed, 9, [], [], [3, 4], [42]));
        RecordingNavigationService navigation = new();
        AlbumViewModel sut = BuildViewModel(navigation);
        sut.SetData(new AlbumDto { Id = 42, Name = "Source" });
        List<(string Kind, long Id, ActionType Action)> sent = [];
        _messenger.Subscribe<AlbumUpdateMessage>(m => sent.Add(("album", m.Id, m.Action)));
        _messenger.Subscribe<ArtistUpdateMessage>(m => sent.Add(("artist", m.Id, m.Action)));

        // Act
        await sut.MergeWithCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([9L], navigation.NavigatedAlbums);
        Assert.Contains(("album", 42L, ActionType.Delete), sent);
        Assert.Contains(("album", 9L, ActionType.Update), sent);
        Assert.DoesNotContain(("album", 42L, ActionType.Update), sent);
        Assert.Contains(("artist", 3L, ActionType.Update), sent);
        Assert.Contains(("artist", 4L, ActionType.Update), sent);
        Assert.Empty(_mediator.Sent<GetAlbumByIdRequest>());
    }

    [Fact(DisplayName = "merging_another_album_into_the_displayed_one_reloads_it_without_navigating")]
    public async Task MergeWith_WhenTheDisplayedAlbumStays_ShouldReloadIt()
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _dialogService
            .Setup(d => d.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()))
            .ReturnsAsync(new AlbumTagWriteOutcome(EArtistRenameState.Partial, 42, [], [], [3], [90]));
        _mediator.Setup<GetAlbumByIdRequest, Result<AlbumDto>>()
                 .Returns(Result<AlbumDto>.Ok(new AlbumDto { Id = 42, Name = "Kept" }));
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));
        RecordingNavigationService navigation = new();
        AlbumViewModel sut = BuildViewModel(navigation);
        sut.SetData(new AlbumDto { Id = 42, Name = "Old" });
        List<(string Kind, long Id, ActionType Action)> sent = [];
        _messenger.Subscribe<AlbumUpdateMessage>(m => sent.Add(("album", m.Id, m.Action)));

        // Act
        await sut.MergeWithCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(navigation.NavigatedAlbums);
        Assert.Equal("Kept", sut.Album.Name);
        Assert.True(sut.HasPendingTagWrite);
        Assert.False(sut.CanMerge);
        Assert.Contains(("album", 42L, ActionType.Update), sent);
        Assert.Contains(("album", 90L, ActionType.Delete), sent);
    }

    [Theory(DisplayName = "a_blocked_or_cancelled_merge_changes_nothing_on_the_page")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MergeWith_WhenNothingWasMerged_ShouldSendNoMessage(bool blocked)
    {
        // Arrange
        _appOptions.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        AlbumTagWriteOutcome? outcome = blocked ? new AlbumTagWriteOutcome(EArtistRenameState.Blocked, 9, [], [], [], []) : null;
        _dialogService
            .Setup(d => d.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()))
            .ReturnsAsync(outcome);
        RecordingNavigationService navigation = new();
        AlbumViewModel sut = BuildViewModel(navigation);
        sut.SetData(new AlbumDto { Id = 42, Name = "Source" });
        int messages = 0;
        _messenger.Subscribe<AlbumUpdateMessage>(_ => messages++);

        // Act
        await sut.MergeWithCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, messages);
        Assert.Empty(navigation.NavigatedAlbums);
        Assert.Empty(_mediator.Sent<GetAlbumByIdRequest>());
    }

    [Theory(DisplayName = "when_the_album_has_a_country_code_then_the_country_flag_is_shown")]
    [InlineData("GB", true)]
    [InlineData("", false)]
    public void HasCountry_ShouldFollowTheCountryCode(string countryCode, bool expected)
    {
        // Arrange
        AlbumViewModel sut = BuildViewModel();

        // Act
        sut.SetData(new AlbumDto { Id = 42, CountryCode = countryCode });

        // Assert
        Assert.Equal(expected, sut.HasCountry);
    }
}