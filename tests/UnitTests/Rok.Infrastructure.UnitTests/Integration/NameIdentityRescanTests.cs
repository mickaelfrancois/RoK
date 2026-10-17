using Dapper;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Services;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Runs the real library import on folders to check that artist, genre and album identity ignore case and disc subfolders.
/// </summary>
public sealed class NameIdentityRescanTests
{
    [Fact(DisplayName = "import_merges_artist_and_genre_case_variants_and_keeps_the_first_spelling")]
    public async Task Import_WhenTagsDifferOnlyByCase_ShouldCreateOneArtistAndOneGenre()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("case", "a1", "AC/DC", "Case Album", "Rock");
        harness.AddFile("case", "a2", "Ac/Dc", "Case Album", "rock");

        // Act
        await harness.ImportTwiceAsync();

        // Assert
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Artists"));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Genres"));
        Assert.Equal("AC/DC", harness.Scalar<string>("SELECT name FROM Artists"));
        Assert.Equal("Rock", harness.Scalar<string>("SELECT name FROM Genres"));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Albums"));
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "rescan_keeps_entities_when_a_tag_only_changes_case")]
    public async Task Rescan_WhenATagChangesOnlyByCase_ShouldCreateAndDeleteNothing()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("recase", "c1", "AC/DC", "Recase Album", "Rock");
        harness.AddFile("recase", "c2", "AC/DC", "Recase Album", "Rock");

        await harness.ImportTwiceAsync();

        long artistId = harness.Scalar<long>("SELECT id FROM Artists");
        long genreId = harness.Scalar<long>("SELECT id FROM Genres");
        long albumId = harness.Scalar<long>("SELECT id FROM Albums");

        // Act
        harness.Edit("c2", artist: "Ac/Dc", genre: "rock", title: "Retitled", days: 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal(0, rescan.ArtistsImported);
        Assert.Equal(0, rescan.GenresImported);
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.ArtistsDeleted);
        Assert.Equal(0, rescan.GenresDeleted);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal([artistId], harness.Ids("Artists"));
        Assert.Equal([genreId], harness.Ids("Genres"));
        Assert.Equal([albumId], harness.Ids("Albums"));
        Assert.Equal("AC/DC", harness.Scalar<string>("SELECT name FROM Artists"));
        Assert.Equal("Rock", harness.Scalar<string>("SELECT name FROM Genres"));
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "homonymous_compilations_in_two_folders_stay_two_albums_through_a_rescan")]
    public async Task Import_WhenTwoCompilationsShareAName_ShouldKeepTwoAlbums()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("hits-one", "h1a", "Artist One", "Greatest Hits", "Rock");
        harness.AddFile("hits-one", "h1b", "Artist Two", "Greatest Hits", "Rock");
        harness.AddFile("hits-two", "h2a", "Artist Three", "Greatest Hits", "Rock");
        harness.AddFile("hits-two", "h2b", "Artist Four", "Greatest Hits", "Rock");

        // Act
        await harness.ImportTwiceAsync();

        // Assert
        List<long> albums = harness.Ids("Albums");

        Assert.Equal(2, albums.Count);
        Assert.Equal(harness.AlbumIdOf("h1a"), harness.AlbumIdOf("h1b"));
        Assert.Equal(harness.AlbumIdOf("h2a"), harness.AlbumIdOf("h2b"));
        Assert.NotEqual(harness.AlbumIdOf("h1a"), harness.AlbumIdOf("h2a"));

        harness.Edit("h1a", title: "Retitled", days: 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled file must be processed again");
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(albums, harness.Ids("Albums"));
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "compilation_split_on_cd1_cd2_stays_one_album_through_a_rescan")]
    public async Task Import_WhenACompilationIsSplitOnDiscFolders_ShouldKeepOneAlbum()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile(Path.Combine("boxset", "CD1"), "d1a", "Artist One", "Boxset", "Rock");
        harness.AddFile(Path.Combine("boxset", "CD1"), "d1b", "Artist Two", "Boxset", "Rock");
        harness.AddFile(Path.Combine("boxset", "CD2"), "d2a", "Artist Three", "Boxset", "Rock");
        harness.AddFile(Path.Combine("boxset", "CD2"), "d2b", "Artist One", "Boxset", "Rock");

        // Act
        await harness.ImportTwiceAsync();

        // Assert
        List<long> albums = harness.Ids("Albums");

        Assert.Single(albums);
        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums"));

        harness.Edit("d2a", title: "Retitled", days: 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled file must be processed again");
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(albums, harness.Ids("Albums"));
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "rescan_reassigning_track_fixes_previous_artist_album_and_genre_counts")]
    public async Task Rescan_WhenATrackMovesToAnotherArtist_ShouldFixThePreviousCounters()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("move", "m1", "Artist X", "Album X", "Rock");
        harness.AddFile("move", "m2", "Artist X", "Album X", "Rock");
        harness.AddFile("other", "m3", "Artist Y", "Album Y", "Jazz");

        await harness.ImportTwiceAsync();

        long artistX = harness.ArtistIdOf("m1");
        long albumX = harness.AlbumIdOf("m1");
        long genreRock = harness.GenreIdOf("m1");

        Assert.Equal(2, harness.Scalar<int>("SELECT trackCount FROM Artists WHERE id = @artistX", new { artistX }));

        // Act
        harness.Edit("m2", artist: "Artist Y", album: "Album Y", genre: "Jazz", days: 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal(artistX, harness.ArtistIdOf("m1"));
        Assert.NotEqual(artistX, harness.ArtistIdOf("m2"));
        Assert.Equal(1, harness.Scalar<int>("SELECT trackCount FROM Artists WHERE id = @artistX", new { artistX }));
        Assert.Equal(1, harness.Scalar<int>("SELECT trackCount FROM Albums WHERE id = @albumX", new { albumX }));
        Assert.Equal(1, harness.Scalar<int>("SELECT trackCount FROM Genres WHERE id = @genreRock", new { genreRock }));
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "rescan_without_change_creates_and_deletes_no_entity")]
    public async Task Rescan_WhenNothingChangesButOneTitle_ShouldCreateAndDeleteNoEntity()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("stable", "s1", "Stable Artist", "Stable Album", "Rock");
        harness.AddFile("stable", "s2", "Stable Artist", "Stable Album", "Rock");
        harness.AddFile("mixed", "s3", "Artist One", "Mixed Album", "Jazz");
        harness.AddFile("mixed", "s4", "Artist Two", "Mixed Album", "Jazz");

        await harness.ImportTwiceAsync();

        List<long> artists = harness.Ids("Artists");
        List<long> albums = harness.Ids("Albums");
        List<long> genres = harness.Ids("Genres");

        // Act
        harness.Edit("s1", title: "Retitled", days: 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled file must be processed again");
        Assert.Equal(0, rescan.ArtistsImported);
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.GenresImported);
        Assert.Equal(0, rescan.ArtistsDeleted);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(0, rescan.GenresDeleted);
        Assert.Equal(artists, harness.Ids("Artists"));
        Assert.Equal(albums, harness.Ids("Albums"));
        Assert.Equal(genres, harness.Ids("Genres"));
        harness.AssertCountersMatchTracks();
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("NameIdentityRescanTests_");
        private readonly TrackRepository _trackRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistRepository _artistRepository;
        private readonly GenreRepository _genreRepository;
        private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
        private readonly LibraryWriteGate _writeGate = new();
        private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true };
        private readonly Dictionary<string, string> _paths = [];

        public Harness()
        {
            _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

            _options.SetCachePath(Path.Combine(_root.FullName, "cache"));

            _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
            _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
            _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
            _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
        }

        private string LibraryFolder => Path.Combine(_root.FullName, "library");

        public T Scalar<T>(string sql, object? args = null)
        {
            return _fixture.Connection.ExecuteScalar<T>(sql, args)!;
        }

        public List<long> Ids(string table)
        {
            return [.. _fixture.Connection.Query<long>($"SELECT id FROM {table} ORDER BY id")];
        }

        public void AddFile(string folderName, string name, string artist, string album, string genre)
        {
            string path = FlacTagFixture.CreateWithArtists(Path.Combine(LibraryFolder, folderName), [], [artist], name + ".flac");

            using (TagLib.File file = TagLib.File.Create(path))
            {
                file.Tag.Title = "Title " + name;
                file.Tag.Album = album;
                file.Tag.Genres = [genre];

                file.Save();
            }

            _paths[name] = path;
        }

        public void Edit(string name, string? artist = null, string? album = null, string? genre = null, string? title = null, int days = 2)
        {
            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                if (artist is not null)
                    file.Tag.Performers = [artist];

                if (album is not null)
                    file.Tag.Album = album;

                if (genre is not null)
                    file.Tag.Genres = [genre];

                if (title is not null)
                    file.Tag.Title = title;

                file.Save();
            }

            File.SetLastWriteTimeUtc(_paths[name], DateTime.UtcNow.AddDays(days));
        }

        public long AlbumIdOf(string name)
        {
            return TrackColumn("albumId", name);
        }

        public long ArtistIdOf(string name)
        {
            return TrackColumn("artistId", name);
        }

        public long GenreIdOf(string name)
        {
            return TrackColumn("genreId", name);
        }

        public void AssertCountersMatchTracks()
        {
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Artists a WHERE a.trackCount <> (SELECT COUNT(*) FROM Tracks t WHERE t.artistId = a.id)"));
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Albums a WHERE a.trackCount <> (SELECT COUNT(*) FROM Tracks t WHERE t.albumId = a.id)"));
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Genres g WHERE g.trackCount <> (SELECT COUNT(*) FROM Tracks t WHERE t.genreId = g.id)"));
        }

        public async Task ImportTwiceAsync()
        {
            await ImportAsync();
            await ImportAsync();
        }

        public async Task<ImportStatisticsDto> ImportAsync()
        {
            Mock<IFolderResolver> folderResolver = new();
            folderResolver.Setup(r => r.GetPathFromTokenAsync("lib")).ReturnsAsync([LibraryFolder]);

            ImportService import = CreateImportService(folderResolver.Object);

            await import.ImportAsync(CancellationToken.None);

            return import.Statistics;
        }

        public void Dispose()
        {
            _fixture.Dispose();

            try
            {
                _root.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private long TrackColumn(string column, string name)
        {
            return Scalar<long>($"SELECT {column} FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}.flac" });
        }

        private ImportService CreateImportService(IFolderResolver folderResolver)
        {
            TimeProvider time = TimeProvider.System;
            Mock<IMessenger> messenger = new();
            Mock<IMediator> mediator = new();
            Mock<ILyricsService> lyrics = new();
            Mock<ITelemetryClient> telemetry = new();

            TrackImport trackImport = new(_trackRepository);
            AlbumImport albumImport = new(_albumRepository, time);
            ArtistImport artistImport = new(_artistRepository, time);
            GenreImport genreImport = new(_genreRepository, time);
            ImportTrackingService tracking = new();
            ImportMessageThrottler throttler = new();
            FileSystemService fileSystem = new(NullLogger<FileSystemService>.Instance);

            FolderImportProcessor folderProcessor = new(
                _options,
                trackImport,
                artistImport,
                albumImport,
                genreImport,
                fileSystem,
                new TrackFileProcessor(_tagService, NullLogger<TrackFileProcessor>.Instance),
                new TrackMetadataService(trackImport, NullLogger<TrackMetadataService>.Instance, time),
                new EmbeddedLyricsImporter(lyrics.Object, NullLogger<EmbeddedLyricsImporter>.Instance),
                tracking,
                _tagService,
                throttler,
                messenger.Object,
                NullLogger<FolderImportProcessor>.Instance);

            PostImportDominantColorTask dominantColor = new(
                Mock.Of<IDominantColorCalculator>(),
                _albumRepository,
                Mock.Of<IAlbumPictureService>(),
                _artistRepository,
                Mock.Of<IArtistPictureService>(),
                NullLogger<PostImportDominantColorTask>.Instance);

            PostImportApiEnrichmentTask apiEnrichment = new(
                artistImport,
                albumImport,
                Mock.Of<IArtistApiService>(),
                Mock.Of<IAlbumApiService>(),
                Mock.Of<IArtistPictureService>(),
                Mock.Of<IAlbumPictureService>(),
                Mock.Of<IBackdropPicture>(),
                mediator.Object,
                NullLogger<PostImportApiEnrichmentTask>.Instance);

            return new ImportService(
                folderResolver,
                _options,
                new Statistics(_trackRepository, _albumRepository, _artistRepository, _genreRepository, NullLogger<Statistics>.Instance),
                trackImport,
                genreImport,
                albumImport,
                artistImport,
                new CleanLibraryService(_trackRepository, _artistRepository, _albumRepository, _genreRepository, NullLogger<CleanLibraryService>.Instance),
                telemetry.Object,
                new ImportProgressService(messenger.Object),
                tracking,
                fileSystem,
                folderProcessor,
                dominantColor,
                apiEnrichment,
                throttler,
                _writeGate,
                NullLogger<ImportService>.Instance);
        }
    }
}