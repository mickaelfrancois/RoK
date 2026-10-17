using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Services;
using Rok.Domain.Enums;
using Rok.Import;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public sealed class FolderImportProcessorTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("FolderImportProcessorTests_");
    private readonly DateTime _fileDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ITrackRepository> _trackRepository = new();
    private readonly Mock<ITagService> _tagService = new();
    private readonly ImportTrackingService _tracking = new();
    private readonly TrackImport _trackImport;
    private readonly FolderImportProcessor _sut;

    public FolderImportProcessorTests()
    {
        _tagService.Setup(t => t.FillBasicProperties(It.IsAny<string>(), It.IsAny<TrackFile>()))
            .Callback<string, TrackFile>((path, track) =>
            {
                track.FullPath = path;
                track.FileDateModified = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            });

        FakeTimeProvider time = new();
        _trackImport = new TrackImport(_trackRepository.Object);

        _sut = new FolderImportProcessor(
            Mock.Of<IAppOptions>(),
            _trackImport,
            new ArtistImport(Mock.Of<IArtistRepository>(), time),
            new AlbumImport(Mock.Of<IAlbumRepository>(), time),
            new GenreImport(Mock.Of<IGenreRepository>(), time),
            new FileSystemService(NullLogger<FileSystemService>.Instance),
            new TrackFileProcessor(_tagService.Object, NullLogger<TrackFileProcessor>.Instance),
            new TrackMetadataService(_trackImport, NullLogger<TrackMetadataService>.Instance, time),
            new EmbeddedLyricsImporter(Mock.Of<ILyricsService>(), NullLogger<EmbeddedLyricsImporter>.Instance),
            _tracking,
            _tagService.Object,
            new ImportMessageThrottler(),
            Mock.Of<IMessenger>(),
            NullLogger<FolderImportProcessor>.Instance);
    }

    [Fact(DisplayName = "when_a_new_file_comes_first_the_following_known_tracks_are_still_marked_read_after_cancellation")]
    public async Task ImportFolder_NewFileFirstAndCancelled_ShouldMarkEveryKnownTrackRead()
    {
        // Arrange
        string[] paths = CreateFiles("a-new", "b-known", "c-known");
        await LoadCacheAsync(Known(2, paths[1], _fileDate), Known(3, paths[2], _fileDate));

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        // Act
        await _sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), cancellation.Token);

        // Assert
        Assert.Equal([2L, 3L], _tracking.GetTrackedIds().Order());
    }

    [Fact(DisplayName = "when_the_first_file_is_updated_the_following_unchanged_tracks_are_still_marked_read")]
    public async Task ImportFolder_FirstFileModified_ShouldMarkEveryKnownTrackRead()
    {
        // Arrange
        string[] paths = CreateFiles("a-stale", "b-known", "c-known");
        await LoadCacheAsync(
            Known(1, paths[0], _fileDate.AddDays(-1)),
            Known(2, paths[1], _fileDate),
            Known(3, paths[2], _fileDate));

        // Act
        await _sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L, 3L], _tracking.GetTrackedIds().Order());
    }

    [Fact(DisplayName = "when_a_middle_file_is_updated_every_known_track_is_still_marked_read")]
    public async Task ImportFolder_MiddleFileModified_ShouldMarkEveryKnownTrackRead()
    {
        // Arrange
        CreateFiles("a-known", "b-known", "c-known");
        List<string> ordered = EnumerationOrder();

        Assert.Equal(3, ordered.Count);

        await LoadCacheAsync(
            Known(1, ordered[0], _fileDate),
            Known(2, ordered[1], _fileDate.AddDays(-1)),
            Known(3, ordered[2], _fileDate));

        // Act
        await _sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L, 3L], _tracking.GetTrackedIds().Order());
        _tagService.Verify(t => t.FillMusicProperties(It.IsAny<string>(), It.IsAny<TrackFile>()), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "mark_read_keeps_the_date_criterion_and_marks_every_known_track")]
    public async Task MarkReadAndCheckImport_ShouldKeepTheDateCriterion_AndMarkEveryKnownTrack()
    {
        // Arrange
        string[] paths = CreateFiles("a-equal", "b-newer-in-database");
        await LoadCacheAsync(Known(1, paths[0], _fileDate), Known(2, paths[1], _fileDate.AddDays(1)));
        List<TrackFile> files = FilesOf(paths);

        // Act
        bool unchangedRequiresImport = _sut.MarkReadAndCheckImport(files);

        // Assert
        Assert.False(unchangedRequiresImport);
        Assert.Equal([1L, 2L], _tracking.GetTrackedIds().Order());

        // Arrange
        await LoadCacheAsync(Known(1, paths[0], _fileDate), Known(2, paths[1], _fileDate.AddTicks(-1)));

        // Act
        bool changedRequiresImport = _sut.MarkReadAndCheckImport(files);

        // Assert
        Assert.True(changedRequiresImport);
        Assert.Equal([1L, 2L], _tracking.GetTrackedIds().Order());
    }

    [Fact(DisplayName = "when_updating_a_track_throws_every_known_track_is_still_marked_read")]
    public async Task ImportFolder_UpdateThrows_ShouldMarkEveryKnownTrackRead()
    {
        // Arrange
        string[] paths = CreateFiles("a-stale", "b-stale", "c-known");
        await LoadCacheAsync(
            Known(1, paths[0], _fileDate.AddDays(-1)),
            Known(2, paths[1], _fileDate.AddDays(-1)),
            Known(3, paths[2], _fileDate));

        _trackRepository.Setup(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .ThrowsAsync(new InvalidOperationException("update failed"));

        // Act
        await _sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L, 3L], _tracking.GetTrackedIds().Order());
    }

    [Fact(DisplayName = "when_nothing_changed_in_the_folder_every_known_track_is_marked_read_and_nothing_is_processed")]
    public async Task ImportFolder_NothingChanged_ShouldMarkEveryKnownTrackReadWithoutUpdate()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        await LoadCacheAsync(Known(1, paths[0], _fileDate), Known(2, paths[1], _fileDate));

        // Act
        await _sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L], _tracking.GetTrackedIds().Order());
        _tagService.Verify(t => t.FillMusicProperties(It.IsAny<string>(), It.IsAny<TrackFile>()), Times.Never);
    }

    [Fact(DisplayName = "reconcile_marks_old_and_new_artist_genre_and_album_as_updated")]
    public async Task ImportFolder_CompilationBecomesSingleArtist_ShouldMarkOldAndNewArtistGenreAndAlbum()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 1, GenreId = 5, IsCompilation = true },
            _ => "Artist B",
            Known(1, paths[0], _fileDate.AddDays(-1), artistId: 1, albumId: 10),
            Known(2, paths[1], _fileDate.AddDays(-1), artistId: 2, albumId: 10));

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        albumRepository.Verify(r => r.UpdateCompilationAsync(10, false, 2, Rok.Domain.Enums.ECompilationSource.Derived, RepositoryConnectionKind.Background), Times.Once);
        Assert.Contains(1L, _tracking.GetUpdatedArtists());
        Assert.Contains(2L, _tracking.GetUpdatedArtists());
        Assert.Contains(5L, _tracking.GetUpdatedGenres());
        Assert.Contains(10L, _tracking.GetUpdatedAlbums());
    }

    [Fact(DisplayName = "reconcile_writes_nothing_when_the_state_is_unchanged")]
    public async Task ImportFolder_StateUnchanged_ShouldNotUpdateTheAlbum()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 1, GenreId = 5, IsCompilation = false },
            _ => "Artist A",
            Known(1, paths[0], _fileDate.AddDays(-1), artistId: 1, albumId: 10),
            Known(2, paths[1], _fileDate.AddDays(-1), artistId: 1, albumId: 10));

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        VerifyNoCompilationUpdate(albumRepository);
        Assert.Empty(_tracking.GetUpdatedGenres());
    }

    [Fact(DisplayName = "reconcile_keeps_a_manual_compilation_flag_when_the_folder_state_is_unchanged")]
    public async Task ImportFolder_ManualCompilationFlag_ShouldNotUpdateTheAlbum()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 1, GenreId = 5, IsCompilation = true },
            _ => "Artist A",
            Known(1, paths[0], _fileDate.AddDays(-1), artistId: 1, albumId: 10),
            Known(2, paths[1], _fileDate.AddDays(-1), artistId: 1, albumId: 10));

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        VerifyNoCompilationUpdate(albumRepository);
    }

    [Fact(DisplayName = "reconcile_moves_a_normal_album_to_the_new_artist_of_a_single_artist_folder")]
    public async Task ImportFolder_SingleArtistFolderRetagged_ShouldMoveTheAlbumToTheNewArtist()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 1, GenreId = 5, IsCompilation = false },
            _ => "Artist B",
            Known(1, paths[0], _fileDate.AddDays(-1), artistId: 1, albumId: 10),
            Known(2, paths[1], _fileDate.AddDays(-1), artistId: 1, albumId: 10));

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        albumRepository.Verify(r => r.UpdateCompilationAsync(10, false, 2, Rok.Domain.Enums.ECompilationSource.Derived, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "reconcile_keeps_the_compilation_when_a_track_outside_the_folder_has_another_artist")]
    public async Task ImportFolder_OtherFolderKeepsAnotherArtist_ShouldKeepTheCompilation()
    {
        // Arrange
        string[] paths = CreateFiles("a-known", "b-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 1, GenreId = 5, IsCompilation = true },
            _ => "Artist B",
            Known(1, paths[0], _fileDate.AddDays(-1), artistId: 1, albumId: 10),
            Known(2, paths[1], _fileDate.AddDays(-1), artistId: 2, albumId: 10),
            Known(3, Path.Combine(Path.GetTempPath(), "elsewhere.mp3"), _fileDate, artistId: 1, albumId: 10));

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        VerifyNoCompilationUpdate(albumRepository);
    }

    [Fact(DisplayName = "pending_old_artist_file_does_not_switch_the_album_to_compilation")]
    public async Task ImportFolder_PendingFileKeepsOldArtist_ShouldNotSwitchTheAlbumToCompilation()
    {
        // Arrange
        string[] paths = CreateFiles("a-written", "b-pending");
        PendingScenario scenario = await BuildPendingScenarioAsync(
            paths,
            file => file.Artist = file.FullPath == paths[1] ? "Old Artist" : "Artist A",
            new PendingTagWrite(2, ETagWriteFields.Artist));

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        VerifyNoCompilationUpdate(scenario.Albums);
        scenario.Artists.Verify(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "old_artist_file_without_pending_row_still_switches_the_album_to_compilation")]
    public async Task ImportFolder_OldArtistFileWithoutPendingRow_ShouldSwitchTheAlbumToCompilation()
    {
        // Arrange
        string[] paths = CreateFiles("a-written", "b-stale");
        PendingScenario scenario = await BuildPendingScenarioAsync(
            paths,
            file => file.Artist = file.FullPath == paths[1] ? "Old Artist" : "Artist A");

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        scenario.Albums.Verify(
            r => r.UpdateCompilationAsync(10, true, It.IsAny<long?>(), It.IsAny<Rok.Domain.Enums.ECompilationSource>(), RepositoryConnectionKind.Background),
            Times.Once);
    }

    [Fact(DisplayName = "pending_renamed_file_never_creates_the_old_album")]
    public async Task ImportFolder_PendingFileKeepsOldAlbumName_ShouldNotCreateAnAlbum()
    {
        // Arrange
        string[] paths = CreateFiles("a-pending", "b-pending");
        PendingScenario scenario = await BuildPendingScenarioAsync(
            paths,
            file => file.Album = "Old Name",
            new PendingTagWrite(1, ETagWriteFields.Album),
            new PendingTagWrite(2, ETagWriteFields.Album));

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        scenario.Albums.Verify(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "renamed_file_without_pending_row_creates_the_old_album")]
    public async Task ImportFolder_OldAlbumNameWithoutPendingRow_ShouldCreateAnAlbum()
    {
        // Arrange
        string[] paths = CreateFiles("a-stale", "b-stale");
        PendingScenario scenario = await BuildPendingScenarioAsync(paths, file => file.Album = "Old Name");

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        scenario.Albums.Verify(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "pending_genre_and_disc_are_not_reverted_by_the_file")]
    public async Task ImportFolder_PendingFileKeepsOldGenreAndDisc_ShouldLeaveTheTrackUnchanged()
    {
        // Arrange
        string[] paths = CreateFiles("a-pending", "b-known");
        PendingScenario scenario = await BuildPendingScenarioAsync(
            paths,
            file =>
            {
                file.Genre = file.FullPath == paths[0] ? "Old Genre" : "New Genre";
                file.Disc = file.FullPath == paths[0] ? 1 : 2;
            },
            new PendingTagWrite(1, ETagWriteFields.Genre | ETagWriteFields.Disc));

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        scenario.Genres.Verify(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "old_disc_file_without_pending_row_updates_the_track")]
    public async Task ImportFolder_OldDiscWithoutPendingRow_ShouldUpdateTheTrack()
    {
        // Arrange
        string[] paths = CreateFiles("a-stale", "b-known");
        PendingScenario scenario = await BuildPendingScenarioAsync(
            paths,
            file => file.Disc = file.FullPath == paths[0] ? 1 : 2);

        // Act
        await scenario.Sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact(DisplayName = "homonymous_compilations_in_two_folders_stay_two_albums")]
    public async Task ImportFolder_HomonymousCompilationsInTwoFolders_ShouldCreateTwoAlbums()
    {
        // Arrange
        long nextId = 100;
        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => nextId++);
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Unrelated", ArtistId = 1 },
            path => path.Contains("a-one") || path.Contains("c-one") ? "Artist A" : "Artist B");
        string firstFolder = CreateFilesIn("Hits 1", "a-one", "b-one");
        string secondFolder = CreateFilesIn("Hits 2", "c-one", "d-one");

        // Act
        await sut.ImportFolderAsync(firstFolder, new ImportStatisticsDto(), CancellationToken.None);
        await sut.ImportFolderAsync(secondFolder, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        albumRepository.Verify(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "compilation_split_on_disc_subfolders_stays_one_album")]
    public async Task ImportFolder_CompilationSplitOnDiscSubfolders_ShouldCreateOneAlbum()
    {
        // Arrange
        long nextId = 100;
        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => nextId++);
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Unrelated", ArtistId = 1 },
            path => path.Contains("a-one") || path.Contains("c-one") ? "Artist A" : "Artist B");
        string firstDisc = CreateFilesIn(Path.Combine("Hits", "CD1"), "a-one", "b-one");
        string secondDisc = CreateFilesIn(Path.Combine("Hits", "CD2"), "c-one", "d-one");

        // Act
        await sut.ImportFolderAsync(firstDisc, new ImportStatisticsDto(), CancellationToken.None);
        await sut.ImportFolderAsync(secondDisc, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        albumRepository.Verify(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "reassigned_track_marks_previous_artist_album_genre")]
    public async Task ImportFolder_ReassignedTrack_ShouldMarkPreviousArtistAlbumAndGenre()
    {
        // Arrange
        string[] paths = CreateFiles("a-known");
        Mock<IAlbumRepository> albumRepository = new();
        FolderImportProcessor sut = await BuildReconcilingProcessorAsync(
            albumRepository,
            new AlbumEntity { Id = 10, Name = "Album", ArtistId = 2, IsCompilation = false },
            _ => "Artist B",
            new TrackEntity { Id = 1, MusicFile = paths[0], FileDate = _fileDate.AddDays(-1), ArtistId = 1, AlbumId = 99, GenreId = 5 });

        // Act
        await sut.ImportFolderAsync(_folder.FullName, new ImportStatisticsDto(), CancellationToken.None);

        // Assert
        Assert.Contains(1L, _tracking.GetUpdatedArtists());
        Assert.Contains(2L, _tracking.GetUpdatedArtists());
        Assert.Contains(99L, _tracking.GetUpdatedAlbums());
        Assert.Contains(10L, _tracking.GetUpdatedAlbums());
        Assert.Contains(5L, _tracking.GetUpdatedGenres());
    }

    private string CreateFilesIn(string relativeFolder, params string[] names)
    {
        string folder = Path.Combine(_folder.FullName, relativeFolder);

        Directory.CreateDirectory(folder);

        foreach (string name in names)
        {
            string path = Path.Combine(folder, name + ".mp3");

            File.WriteAllBytes(path, [0]);
            File.SetLastWriteTimeUtc(path, _fileDate);
        }

        return folder;
    }

    private static TrackEntity Known(long id, string path, DateTime fileDate, long? artistId = null, long? albumId = null)
    {
        return new TrackEntity { Id = id, MusicFile = path, FileDate = fileDate, ArtistId = artistId, AlbumId = albumId };
    }

    private static void VerifyNoCompilationUpdate(Mock<IAlbumRepository> albumRepository)
    {
        albumRepository.Verify(
            r => r.UpdateCompilationAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<Rok.Domain.Enums.ECompilationSource>(), It.IsAny<RepositoryConnectionKind>()),
            Times.Never);
    }

    private async Task<FolderImportProcessor> BuildReconcilingProcessorAsync(
        Mock<IAlbumRepository> albumRepository,
        AlbumEntity album,
        Func<string, string> artistOfFile,
        params TrackEntity[] tracks)
    {
        _tagService.Setup(t => t.FillMusicProperties(It.IsAny<string>(), It.IsAny<TrackFile>()))
            .Callback<string, TrackFile>((_, file) =>
            {
                file.Album = "Album";
                file.Artist = artistOfFile(file.FullPath);
            });

        albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([album]);

        Mock<IArtistRepository> artistRepository = new();
        artistRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new ArtistEntity { Id = 1, Name = "Artist A" }, new ArtistEntity { Id = 2, Name = "Artist B" }]);

        FakeTimeProvider time = new();
        AlbumImport albumImport = new(albumRepository.Object, time);
        ArtistImport artistImport = new(artistRepository.Object, time);

        await albumImport.LoadCacheAsync();
        await artistImport.LoadCacheAsync();
        await LoadCacheAsync(tracks);

        return new FolderImportProcessor(
            Mock.Of<IAppOptions>(),
            _trackImport,
            artistImport,
            albumImport,
            new GenreImport(Mock.Of<IGenreRepository>(), time),
            new FileSystemService(NullLogger<FileSystemService>.Instance),
            new TrackFileProcessor(_tagService.Object, NullLogger<TrackFileProcessor>.Instance),
            new TrackMetadataService(_trackImport, NullLogger<TrackMetadataService>.Instance, time),
            new EmbeddedLyricsImporter(Mock.Of<ILyricsService>(), NullLogger<EmbeddedLyricsImporter>.Instance),
            _tracking,
            _tagService.Object,
            new ImportMessageThrottler(),
            Mock.Of<IMessenger>(),
            NullLogger<FolderImportProcessor>.Instance);
    }

    private string[] CreateFiles(params string[] names)
    {
        List<string> paths = [];

        foreach (string name in names)
        {
            string path = Path.Combine(_folder.FullName, name + ".mp3");

            File.WriteAllBytes(path, [0]);
            File.SetLastWriteTimeUtc(path, _fileDate);

            paths.Add(path);
        }

        return [.. paths];
    }

    private List<TrackFile> FilesOf(IEnumerable<string> paths)
    {
        List<TrackFile> files = [];

        foreach (string path in paths)
        {
            TrackFile file = new();
            _tagService.Object.FillBasicProperties(path, file);
            files.Add(file);
        }

        return files;
    }

    private List<string> EnumerationOrder()
    {
        FileSystemService fileSystem = new(NullLogger<FileSystemService>.Instance);

        return fileSystem.GetMusicFiles(_folder.FullName, _tagService.Object.FillBasicProperties).Select(f => f.FullPath).ToList();
    }

    private Task LoadCacheAsync(params TrackEntity[] tracks)
    {
        _trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);

        return _trackImport.LoadCacheAsync();
    }

    private sealed record PendingScenario(
        FolderImportProcessor Sut,
        Mock<IAlbumRepository> Albums,
        Mock<IArtistRepository> Artists,
        Mock<IGenreRepository> Genres);

    private async Task<PendingScenario> BuildPendingScenarioAsync(
        string[] paths,
        Action<TrackFile> fileValues,
        params PendingTagWrite[] pending)
    {
        _tagService.Setup(t => t.FillMusicProperties(It.IsAny<string>(), It.IsAny<TrackFile>()))
            .Callback<string, TrackFile>((_, file) =>
            {
                file.Album = "Edited Album";
                file.Artist = "Artist A";
                file.Genre = "New Genre";
                file.Disc = 2;
                fileValues(file);
            });

        Mock<IAlbumRepository> albums = new();
        albums.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new AlbumEntity { Id = 10, Name = "Edited Album", ArtistId = 1, GenreId = 7, IsCompilation = false }]);

        Mock<IArtistRepository> artists = new();
        artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new ArtistEntity { Id = 1, Name = "Artist A" }]);

        Mock<IGenreRepository> genres = new();
        genres.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new GenreEntity { Id = 7, Name = "New Genre" }]);

        FakeTimeProvider time = new();
        AlbumImport albumImport = new(albums.Object, time);
        ArtistImport artistImport = new(artists.Object, time);
        GenreImport genreImport = new(genres.Object, time);

        await albumImport.LoadCacheAsync();
        await artistImport.LoadCacheAsync();
        await genreImport.LoadCacheAsync();

        _trackRepository.Setup(r => r.GetPendingTagWritesAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(pending);

        await LoadCacheAsync(
            StoredInEditedAlbum(1, paths[0]),
            StoredInEditedAlbum(2, paths[1]));

        FolderImportProcessor sut = new(
            Mock.Of<IAppOptions>(),
            _trackImport,
            artistImport,
            albumImport,
            genreImport,
            new FileSystemService(NullLogger<FileSystemService>.Instance),
            new TrackFileProcessor(_tagService.Object, NullLogger<TrackFileProcessor>.Instance),
            new TrackMetadataService(_trackImport, NullLogger<TrackMetadataService>.Instance, time),
            new EmbeddedLyricsImporter(Mock.Of<ILyricsService>(), NullLogger<EmbeddedLyricsImporter>.Instance),
            _tracking,
            _tagService.Object,
            new ImportMessageThrottler(),
            Mock.Of<IMessenger>(),
            NullLogger<FolderImportProcessor>.Instance);

        return new PendingScenario(sut, albums, artists, genres);
    }

    private TrackEntity StoredInEditedAlbum(long id, string path)
    {
        return new TrackEntity
        {
            Id = id,
            MusicFile = path,
            FileDate = _fileDate.AddDays(-1),
            ArtistId = 1,
            AlbumId = 10,
            GenreId = 7,
            ArtistName = "Artist A",
            AlbumName = "Edited Album",
            GenreName = "New Genre",
            Disc = 2,
        };
    }
}