using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class LibraryMetadataRefresherTests
{
    private readonly Mock<ITrackRepository> _trackRepository = new();
    private readonly Mock<IAlbumRepository> _albumRepository = new();
    private readonly Mock<ITagService> _tagService = new();
    private readonly Mock<ILyricsService> _lyricsService = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    public LibraryMetadataRefresherTests()
    {
        _albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([]);
        _fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
    }

    private LibraryMetadataRefresher CreateSut()
    {
        EmbeddedLyricsImporter lyricsImporter = new(_lyricsService.Object, NullLogger<EmbeddedLyricsImporter>.Instance);

        return new LibraryMetadataRefresher(
            _trackRepository.Object,
            _albumRepository.Object,
            _tagService.Object,
            lyricsImporter,
            _fileSystem.Object,
            NullLogger<LibraryMetadataRefresher>.Instance);
    }

    private void SetupTracks(params TrackEntity[] tracks)
    {
        _trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
    }

    private void SetupAlbums(params AlbumEntity[] albums)
    {
        _albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
    }

    // Each invocation of FillProperties gets the tag values for the matching file path.
    private void SetupTagReader(Func<string, Action<TrackFile>> configureByPath)
    {
        _tagService.Setup(s => s.FillProperties(It.IsAny<string>(), It.IsAny<TrackFile>()))
                   .Callback<string, TrackFile>((path, file) => configureByPath(path)(file));
    }

    private void SetupTagReader(Action<TrackFile> configure) => SetupTagReader(_ => configure);

    [Fact(DisplayName = "dry_run_counts_changes_without_writing_to_the_repositories")]
    public async Task DryRun_DoesNotPersist()
    {
        // Arrange
        SetupTracks(new TrackEntity { Id = 1, MusicFile = @"C:\music\song.mp3", AlbumId = 10 });
        SetupAlbums(new AlbumEntity { Id = 10 });
        SetupTagReader(file =>
        {
            file.Disc = 2;
            file.SampleRate = 44100;
            file.MusicbrainzAlbumID = "release-123";
        });

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: false);

        // Assert
        Assert.False(report.Applied);
        Assert.Equal(1, report.TracksScanned);
        Assert.Equal(1, report.TracksUpdated);
        Assert.Equal(1, report.AlbumsUpdated);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _albumRepository.Verify(r => r.UpdateAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "apply_persists_track_and_album_metadata_from_the_tag")]
    public async Task Apply_PersistsTrackAndAlbum()
    {
        // Arrange
        SetupTracks(new TrackEntity { Id = 1, MusicFile = @"C:\music\song.mp3", AlbumId = 10 });
        SetupAlbums(new AlbumEntity { Id = 10 });
        _trackRepository.Setup(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        _albumRepository.Setup(r => r.UpdateAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        SetupTagReader(file =>
        {
            file.Disc = 2;
            file.Bpm = 120;
            file.SampleRate = 44100;
            file.BitsPerSample = 16;
            file.Channels = 2;
            file.DiscCount = 2;
            file.MusicbrainzAlbumID = "release-123";
            file.MusicBrainzReleaseType = "album";
        });

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.True(report.Applied);
        _trackRepository.Verify(r => r.UpdateAsync(
            It.Is<TrackEntity>(t => t.Disc == 2 && t.Bpm == 120 && t.SampleRate == 44100 && t.BitsPerSample == 16 && t.Channels == 2),
            It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _albumRepository.Verify(r => r.UpdateAsync(
            It.Is<AlbumEntity>(a => a.DiscCount == 2 && a.MusicBrainzID == "release-123" && a.MusicBrainzReleaseType == "album"),
            It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "a_present_tag_value_overwrites_a_different_existing_database_value")]
    public async Task PresentTag_OverwritesDifferentValue()
    {
        // Arrange
        TrackEntity track = new() { Id = 1, MusicFile = @"C:\music\song.mp3", Disc = 5 };
        SetupTracks(track);
        _trackRepository.Setup(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        SetupTagReader(file => file.Disc = 2);

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.Equal(1, report.TracksUpdated);
        Assert.Equal(2, track.Disc);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "an_existing_value_is_not_blanked_when_the_tag_has_no_value")]
    public async Task EmptyTag_DoesNotOverwriteExistingValue()
    {
        // Arrange
        TrackEntity track = new() { Id = 1, MusicFile = @"C:\music\song.mp3", Disc = 5, SampleRate = 48000 };
        SetupTracks(track);
        SetupTagReader(_ => { /* tag carries no extended metadata */ });

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.Equal(0, report.TracksUpdated);
        Assert.Equal(5, track.Disc);
        Assert.Equal(48000, track.SampleRate);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "album_metadata_is_aggregated_from_a_later_track_when_the_first_lacks_it")]
    public async Task AlbumMetadata_AggregatedAcrossTracks()
    {
        // Arrange: first track has no album-level disc count, the second one does.
        SetupTracks(
            new TrackEntity { Id = 1, MusicFile = @"C:\music\d1.mp3", AlbumId = 10 },
            new TrackEntity { Id = 2, MusicFile = @"C:\music\d2.mp3", AlbumId = 10 });
        SetupAlbums(new AlbumEntity { Id = 10 });
        _albumRepository.Setup(r => r.UpdateAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        SetupTagReader(path => path.EndsWith("d2.mp3", StringComparison.Ordinal)
            ? file => file.DiscCount = 2
            : _ => { });

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.Equal(1, report.AlbumsUpdated);
        _albumRepository.Verify(r => r.UpdateAsync(It.Is<AlbumEntity>(a => a.DiscCount == 2), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "missing_file_is_counted_and_skipped_without_reading_the_tag")]
    public async Task MissingFile_IsSkipped()
    {
        // Arrange
        SetupTracks(new TrackEntity { Id = 1, MusicFile = @"C:\music\gone.mp3", AlbumId = 10 });
        _fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.Equal(1, report.FilesMissing);
        Assert.Equal(0, report.TracksUpdated);
        _tagService.Verify(s => s.FillProperties(It.IsAny<string>(), It.IsAny<TrackFile>()), Times.Never);
        _trackRepository.Verify(r => r.UpdateAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "embedded_lyrics_sidecar_is_counted_when_no_sidecar_exists")]
    public async Task EmbeddedLyrics_AreCounted()
    {
        // Arrange
        SetupTracks(new TrackEntity { Id = 1, MusicFile = @"C:\music\song.mp3" });
        _lyricsService.Setup(s => s.CheckLyricsFileExists(It.IsAny<string>())).Returns(ELyricsType.None);
        _lyricsService.Setup(s => s.GetPlainLyricsFileName(It.IsAny<string>())).Returns(@"C:\music\song.txt");
        _lyricsService.Setup(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>())).Returns(Task.CompletedTask);
        SetupTagReader(file => file.Lyrics = "Hello world");

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        LibraryMetadataRefreshReport report = await sut.RefreshAsync(apply: true);

        // Assert
        Assert.Equal(1, report.LyricsSidecarsCreated);
        _lyricsService.Verify(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>()), Times.Once);
    }

    [Fact(DisplayName = "progress_is_reported_through_the_end_of_the_tracks_phase")]
    public async Task Progress_IsReported()
    {
        // Arrange
        SetupTracks(
            new TrackEntity { Id = 1, MusicFile = @"C:\music\a.mp3" },
            new TrackEntity { Id = 2, MusicFile = @"C:\music\b.mp3" });
        SetupTagReader(_ => { });

        List<LibraryMetadataRefreshProgress> reports = [];
        Mock<IProgress<LibraryMetadataRefreshProgress>> progress = new();
        progress.Setup(p => p.Report(It.IsAny<LibraryMetadataRefreshProgress>()))
                .Callback<LibraryMetadataRefreshProgress>(reports.Add);

        LibraryMetadataRefresher sut = CreateSut();

        // Act
        await sut.RefreshAsync(apply: false, progress.Object);

        // Assert
        Assert.Contains(reports, r => r.Phase == "Tracks" && r.Processed == 2 && r.Total == 2);
    }
}
