using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import;
using Rok.Import.Services;

namespace Rok.ImportTests;

public class TrackMetadataServiceTests
{
    private static TrackFile BuildFile(
        string title = "Title",
        string artist = "Artist",
        string album = "Album",
        string genre = "Genre",
        int? trackNumber = 1,
        long size = 1000,
        TimeSpan? duration = null,
        int bitrate = 320,
        string fullPath = @"C:\music\track.mp3",
        DateTimeOffset? fileDate = null)
    {
        return new TrackFile
        {
            Title = title,
            Artist = artist,
            Album = album,
            Genre = genre,
            TrackNumber = trackNumber,
            Size = size,
            Duration = duration ?? TimeSpan.FromSeconds(180),
            Bitrate = bitrate,
            FullPath = fullPath,
            FileDateModified = fileDate ?? new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero)
        };
    }

    private static TrackEntity BuildTrack(
        long id = 1,
        string title = "Title",
        string artistName = "Artist",
        string albumName = "Album",
        string genreName = "Genre",
        int? trackNumber = 1,
        long size = 1000,
        DateTime? fileDate = null)
    {
        return new TrackEntity
        {
            Id = id,
            Title = title,
            ArtistName = artistName,
            AlbumName = albumName,
            GenreName = genreName,
            TrackNumber = trackNumber,
            Size = size,
            FileDate = fileDate ?? new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    #region AreTrackAndFileEquals

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnTrue_WhenAllFieldsMatch()
    {
        TrackEntity track = BuildTrack();
        TrackFile file = BuildFile();

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.True(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenTitleDiffers()
    {
        TrackEntity track = BuildTrack(title: "Old Title");
        TrackFile file = BuildFile(title: "New Title");

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenArtistDiffers()
    {
        TrackEntity track = BuildTrack(artistName: "Old Artist");
        TrackFile file = BuildFile(artist: "New Artist");

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenAlbumDiffers()
    {
        TrackEntity track = BuildTrack(albumName: "Old Album");
        TrackFile file = BuildFile(album: "New Album");

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenGenreDiffers()
    {
        TrackEntity track = BuildTrack(genreName: "Rock");
        TrackFile file = BuildFile(genre: "Jazz");

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenSizeDiffers()
    {
        TrackEntity track = BuildTrack(size: 1000);
        TrackFile file = BuildFile(size: 2000);

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldReturnFalse_WhenTrackNumberDiffers()
    {
        TrackEntity track = BuildTrack(trackNumber: 1);
        TrackFile file = BuildFile(trackNumber: 2);

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.False(result);
    }

    [Fact]
    public void AreTrackAndFileEquals_ShouldBeCaseInsensitive()
    {
        TrackEntity track = BuildTrack(title: "title", artistName: "artist", albumName: "album", genreName: "genre");
        TrackFile file = BuildFile(title: "TITLE", artist: "ARTIST", album: "ALBUM", genre: "GENRE");

        bool result = TrackMetadataService.AreTrackAndFileEquals(track, file);

        Assert.True(result);
    }

    #endregion

    #region EnsureTrackTimestamps

    [Fact]
    public void EnsureTrackTimestamps_WhenNewTrack_ShouldSetMusicFileAndCreatDate()
    {
        TrackMetadataService service = new(new TrackImport(Mock.Of<ITrackRepository>()), Mock.Of<ILogger<TrackMetadataService>>(), TimeProvider.System);
        TrackEntity track = new() { Id = 0 };
        TrackFile file = BuildFile(fullPath: @"C:\music\song.mp3");

        service.EnsureTrackTimestamps(track, file);

        Assert.Equal(Path.GetFullPath(@"C:\music\song.mp3"), track.MusicFile);
        Assert.NotEqual(default, track.CreatDate);
        Assert.Null(track.EditDate);
    }

    [Fact]
    public void EnsureTrackTimestamps_WhenExistingTrack_ShouldSetEditDateOnly()
    {
        TrackMetadataService service = new(new TrackImport(Mock.Of<ITrackRepository>()), Mock.Of<ILogger<TrackMetadataService>>(), TimeProvider.System);
        TrackEntity track = new() { Id = 42, MusicFile = @"C:\music\song.mp3" };
        TrackFile file = BuildFile(fullPath: @"C:\music\other.mp3");

        service.EnsureTrackTimestamps(track, file);

        Assert.Equal(@"C:\music\song.mp3", track.MusicFile);
        Assert.NotNull(track.EditDate);
    }

    [Fact]
    public void EnsureTrackTimestamps_WhenNewTrack_CreatDateShouldMatchTimeProvider()
    {
        FakeTimeProvider fakeTime = new();
        fakeTime.SetLocalTimeZone(TimeZoneInfo.Local);
        TrackMetadataService service = new(new TrackImport(Mock.Of<ITrackRepository>()), Mock.Of<ILogger<TrackMetadataService>>(), fakeTime);
        TrackEntity track = new() { Id = 0 };
        TrackFile file = BuildFile();

        service.EnsureTrackTimestamps(track, file);

        Assert.Equal(fakeTime.GetLocalNow().DateTime, track.CreatDate);
    }

    #endregion

    #region FillTrackEntity

    [Fact]
    public void FillTrackEntity_ShouldFillAllFields()
    {
        TrackEntity track = new();
        TrackFile file = BuildFile(
            title: "My Song",
            size: 5000,
            bitrate: 256,
            trackNumber: 3,
            duration: TimeSpan.FromSeconds(200));

        TrackMetadataService.FillTrackEntity(track, file, artistId: 10, albumId: 20, genreId: 30);

        Assert.Equal(10, track.ArtistId);
        Assert.Equal(20, track.AlbumId);
        Assert.Equal(30, track.GenreId);
        Assert.Equal("My Song", track.Title);
        Assert.Equal(5000, track.Size);
        Assert.Equal(256, track.Bitrate);
        Assert.Equal(3, track.TrackNumber);
        Assert.Equal(200, track.Duration);
    }

    [Fact]
    public void FillTrackEntity_ShouldAcceptNullIds()
    {
        TrackEntity track = new();
        TrackFile file = BuildFile();

        TrackMetadataService.FillTrackEntity(track, file, artistId: null, albumId: null, genreId: null);

        Assert.Null(track.ArtistId);
        Assert.Null(track.AlbumId);
        Assert.Null(track.GenreId);
    }

    [Fact]
    public void FillTrackEntity_ShouldRoundDurationToNearestSecond()
    {
        TrackEntity track = new();
        TrackFile file = BuildFile(duration: TimeSpan.FromSeconds(99.6));

        TrackMetadataService.FillTrackEntity(track, file, null, null, null);

        Assert.Equal(100, track.Duration);
    }

    [Fact]
    public void FillTrackEntity_ShouldSetFileDate()
    {
        TrackEntity track = new();
        var fileDate = new DateTimeOffset(2023, 6, 15, 10, 30, 0, TimeSpan.Zero);
        TrackFile file = BuildFile(fileDate: fileDate);

        TrackMetadataService.FillTrackEntity(track, file, null, null, null);

        Assert.Equal(fileDate.DateTime, track.FileDate);
    }

    #endregion

    #region ShouldUpdateMetadataAsync

    [Fact]
    public async Task ShouldUpdateMetadataAsync_WhenTrackIsNull_ShouldReturnTrue()
    {
        Mock<ITrackRepository> mockRepo = new();
        TrackImport importTrack = new(mockRepo.Object);
        Mock<ILogger<TrackMetadataService>> mockLogger = new();
        TrackMetadataService service = new(importTrack, mockLogger.Object, TimeProvider.System);

        TrackFile file = BuildFile();

        bool result = await service.ShouldUpdateMetadataAsync(file, null);

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldUpdateMetadataAsync_WhenMetadataDiffers_ShouldReturnTrue()
    {
        Mock<ITrackRepository> mockRepo = new();
        TrackImport importTrack = new(mockRepo.Object);
        Mock<ILogger<TrackMetadataService>> mockLogger = new();
        TrackMetadataService service = new(importTrack, mockLogger.Object, TimeProvider.System);

        TrackEntity track = BuildTrack(title: "Old Title");
        TrackFile file = BuildFile(title: "New Title");

        bool result = await service.ShouldUpdateMetadataAsync(file, track);

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldUpdateMetadataAsync_WhenMetadataAndDateMatch_ShouldReturnFalse()
    {
        Mock<ITrackRepository> mockRepo = new();
        TrackImport importTrack = new(mockRepo.Object);
        Mock<ILogger<TrackMetadataService>> mockLogger = new();
        TrackMetadataService service = new(importTrack, mockLogger.Object, TimeProvider.System);

        var date = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        TrackEntity track = BuildTrack(fileDate: date);
        TrackFile file = BuildFile(fileDate: new DateTimeOffset(date));

        bool result = await service.ShouldUpdateMetadataAsync(file, track);

        Assert.False(result);
    }

    #endregion
}