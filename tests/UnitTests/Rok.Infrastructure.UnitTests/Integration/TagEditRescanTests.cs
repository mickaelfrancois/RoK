using System.Data;
using System.Globalization;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Messaging;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Edits tags through <see cref="TrackTagEditService"/>, then runs the real library import again and checks that the rescan leaves the database as the edit left it.
/// </summary>
public sealed class TagEditRescanTests
{
    private const string EditedFile = "track-a";
    private const string NeighbourFile = "track-b";
    private const string NeighbourRetitled = "Neighbour retitled by another reader";

    private static readonly string[] SnapshotTables = ["Tracks", "Albums", "Artists", "Genres", "PlaylistTracks"];

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_an_edit_creates_updates_and_deletes_nothing_mp3")]
    public async Task Rescan_AfterEditMp3_ShouldChangeNothingButTheNeighbour()
    {
        using Harness harness = new(CreateMp3);

        await AssertRescanChangesNothingAsync(harness);
    }

    [Fact(DisplayName = "rescan_after_an_edit_creates_updates_and_deletes_nothing_flac")]
    public async Task Rescan_AfterEditFlac_ShouldChangeNothingButTheNeighbour()
    {
        using Harness harness = new(CreateFlac);

        await AssertRescanChangesNothingAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "rescan_after_an_edit_creates_updates_and_deletes_nothing_m4a")]
    public async Task Rescan_AfterEditM4a_ShouldChangeNothingButTheNeighbour()
    {
        using Harness harness = new(CreateM4a);

        await AssertRescanChangesNothingAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_track_or_disc_change_keeps_album_and_artist_rows")]
    public async Task Rescan_AfterTrackAndDiscEdit_ShouldKeepAlbumAndArtistRows()
    {
        // Arrange
        using Harness harness = new(CreateMp3);
        await harness.ImportAsync();
        await harness.ImportAsync();
        long editedId = harness.TrackId(EditedFile);
        Snapshot before = harness.TakeSnapshot();

        // Act
        await harness.EditAsync(new TrackTagsEdit(editedId, TrackNumber: 9, Disc: 3));
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(before.Rows("Albums"), after.Rows("Albums"));
        Assert.Equal(before.Rows("Artists"), after.Rows("Artists"));
        Assert.Equal(before.Rows("Genres"), after.Rows("Genres"));
        Assert.Equal("9", after.Value("Tracks", editedId, "trackNumber"));
        Assert.Equal("3", after.Value("Tracks", editedId, "disc"));
        Assert.Equal(before.Value("Tracks", editedId, "albumId"), after.Value("Tracks", editedId, "albumId"));
        Assert.Equal(before.Value("Tracks", editedId, "artistId"), after.Value("Tracks", editedId, "artistId"));
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", harness.TrackId(NeighbourFile), "title"));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_picks_up_disc_composer_and_bpm_changed_by_another_player")]
    public async Task Rescan_AfterAnotherReaderChangedTheTags_ShouldUpdateTheDatabase()
    {
        // Arrange
        using Harness harness = new(CreateMp3);
        await harness.ImportAsync();
        await harness.ImportAsync();
        long editedId = harness.TrackId(EditedFile);

        // Act
        harness.ChangeNumbersWithTagLib(EditedFile, disc: 4, composer: "Other player composer", bpm: 141);
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(1, statistics.TracksUpdated);
        Assert.Equal("4", after.Value("Tracks", editedId, "disc"));
        Assert.Equal("Other player composer", after.Value("Tracks", editedId, "composers"));
        Assert.Equal("141", after.Value("Tracks", editedId, "bpm"));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_edit_keeps_score_listens_and_playlists")]
    public async Task Rescan_AfterEdit_ShouldKeepScoreListensAndPlaylists()
    {
        // Arrange
        using Harness harness = new(CreateMp3);
        await harness.ImportAsync();
        await harness.ImportAsync();
        long editedId = harness.TrackId(EditedFile);
        harness.SetListeningData(editedId);

        // Act
        await harness.EditAsync(new TrackTagsEdit(editedId, "Edited title", 7, 2, "Edited composer", 99));
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal("4", after.Value("Tracks", editedId, "score"));
        Assert.Equal("12", after.Value("Tracks", editedId, "listenCount"));
        Assert.Equal("3", after.Value("Tracks", editedId, "skipCount"));
        Assert.Single(after.Rows("PlaylistTracks"));
        Assert.Equal(editedId.ToString(CultureInfo.InvariantCulture), after.Value("PlaylistTracks", after.Rows("PlaylistTracks").Keys.Single(), "trackId"));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "title_only_edit_behaves_as_before")]
    public async Task Rescan_AfterTitleOnlyEdit_ShouldKeepTheOtherFieldsAndNotUpdateTheTrack()
    {
        // Arrange
        using Harness harness = new(CreateMp3);
        await harness.ImportAsync();
        await harness.ImportAsync();
        long editedId = harness.TrackId(EditedFile);
        Snapshot imported = harness.TakeSnapshot();

        // Act
        await harness.EditAsync(new TrackTagsEdit(editedId, "Only the title"));
        Snapshot edited = harness.TakeSnapshot();
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal("Only the title", edited.Value("Tracks", editedId, "title"));

        foreach (string column in new[] { "trackNumber", "disc", "composers", "bpm", "albumId", "artistId", "genreId" })
        {
            Assert.Equal(imported.Value("Tracks", editedId, column), after.Value("Tracks", editedId, column));
        }

        Assert.Equal(edited.Rows("Tracks")[editedId], after.Rows("Tracks")[editedId]);
    }

    private static async Task AssertRescanChangesNothingAsync(Harness harness)
    {
        // Arrange
        await harness.ImportAsync();
        await harness.ImportAsync();

        long editedId = harness.TrackId(EditedFile);
        long neighbourId = harness.TrackId(NeighbourFile);
        harness.SetListeningData(editedId);

        TrackTagEditResult result = await harness.EditAsync(new TrackTagsEdit(editedId, "Edited title", 7, 2, "Edited composer", 99));
        Snapshot before = harness.TakeSnapshot();

        // Act
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Edited title", before.Value("Tracks", editedId, "title"));
        Assert.Equal("7", before.Value("Tracks", editedId, "trackNumber"));
        Assert.Equal("2", before.Value("Tracks", editedId, "disc"));
        Assert.Equal("Edited composer", before.Value("Tracks", editedId, "composers"));
        Assert.Equal("99", before.Value("Tracks", editedId, "bpm"));

        // Positive control: the folder was processed again, so the neighbour changed by another reader was taken over.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, statistics.TracksUpdated);

        Assert.Equal(0, statistics.TracksImported);
        Assert.Equal(0, statistics.AlbumsImported);
        Assert.Equal(0, statistics.ArtistsImported);
        Assert.Equal(0, statistics.GenresImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(0, statistics.ArtistsDeleted);
        Assert.Equal(0, statistics.GenresDeleted);

        foreach (string table in SnapshotTables)
        {
            Dictionary<long, string> expected = before.Rows(table);
            Dictionary<long, string> actual = after.Rows(table);

            if (table == "Tracks")
            {
                expected.Remove(neighbourId);
                actual.Remove(neighbourId);
            }

            Assert.Equal(expected, actual);
        }
    }

    private static string CreateMp3(string folder, string name)
    {
        string path = Mp3TagFixture.CreateRich(folder, 3, name + ".mp3");

        PrepareTags(path, name);

        return path;
    }

    private static string CreateFlac(string folder, string name)
    {
        string path = FlacTagFixture.CreateRich(folder, name + ".flac");

        PrepareTags(path, name);

        return path;
    }

    private static string CreateM4a(string folder, string name)
    {
        string path = M4aTagFixture.CreateRich(folder, EMp4Layout.MoovBeforeMdat, name + ".m4a");

        PrepareTags(path, name);

        return path;
    }

    private static void PrepareTags(string path, string name)
    {
        using TagLib.File file = TagLib.File.Create(path);

        file.Tag.Title = name == EditedFile ? "Track A" : "Track B";
        file.Tag.Album = "Rescan album";
        file.Tag.Genres = ["Rock"];

        file.Save();
    }

    private sealed class Snapshot(Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables)
    {
        public Dictionary<long, string> Rows(string table)
        {
            return tables[table].ToDictionary(
                row => row.Key,
                row => string.Join("|", row.Value.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}")));
        }

        public string Value(string table, long id, string column)
        {
            return tables[table][id].Single(c => string.Equals(c.Key, column, StringComparison.OrdinalIgnoreCase)).Value;
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("TagEditRescanTests_");
        private readonly TrackRepository _trackRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistRepository _artistRepository;
        private readonly GenreRepository _genreRepository;
        private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
        private readonly TagLibTagWriter _tagWriter;
        private readonly LibraryWriteGate _writeGate = new();
        private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true };
        private readonly Dictionary<string, string> _paths = [];

        public Harness(Func<string, string, string> createFile)
        {
            _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

            _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
            _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
            _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
            _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
            _tagWriter = new TagLibTagWriter(Path.Combine(_root.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance);

            foreach (string name in new[] { EditedFile, NeighbourFile })
            {
                _paths[name] = createFile(LibraryFolder, name);
            }
        }

        private string LibraryFolder => Path.Combine(_root.FullName, "library");

        public long TrackId(string name)
        {
            return _fixture.Connection.ExecuteScalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}%" });
        }

        public async Task<ImportStatisticsDto> ImportAsync()
        {
            Mock<IFolderResolver> folderResolver = new();
            folderResolver.Setup(r => r.GetPathFromTokenAsync("lib")).ReturnsAsync([LibraryFolder]);

            ImportService import = CreateImportService(folderResolver.Object);

            await import.ImportAsync(CancellationToken.None);

            return import.Statistics;
        }

        public async Task<TrackTagEditResult> EditAsync(TrackTagsEdit edit)
        {
            Mock<IPlayerService> player = new();
            Mock<IImport> import = new();
            import.SetupGet(i => i.UpdateInProgress).Returns(false);

            TrackTagEditService service = new(_trackRepository, _tagWriter, player.Object, import.Object, _writeGate, _options, NullLogger<TrackTagEditService>.Instance);

            IReadOnlyList<TrackTagEditResult> results = await service.UpdateAsync([edit], CancellationToken.None);

            return Assert.Single(results);
        }

        public void RetitleNeighbourAsAnotherReader()
        {
            string path = _paths[NeighbourFile];

            using (TagLib.File file = TagLib.File.Create(path))
            {
                file.Tag.Title = NeighbourRetitled;
                file.Save();
            }

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(2));
        }

        public void ChangeNumbersWithTagLib(string name, uint disc, string composer, uint bpm)
        {
            string path = _paths[name];

            using (TagLib.File file = TagLib.File.Create(path))
            {
                file.Tag.Disc = disc;
                file.Tag.Composers = [composer];
                file.Tag.BeatsPerMinute = bpm;
                file.Save();
            }

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(2));
        }

        public void SetListeningData(long trackId)
        {
            _fixture.Connection.Execute("UPDATE Tracks SET score = 4, listenCount = 12, skipCount = 3 WHERE id = @trackId", new { trackId });

            DateTime now = DateTime.UtcNow;
            long playlistId = _fixture.Connection.ExecuteScalar<long>(
                "INSERT INTO Playlists(name, type, creatDate) VALUES ('Rescan playlist', 0, @now); SELECT last_insert_rowid();",
                new { now });

            _fixture.Connection.Execute(
                "INSERT INTO PlaylistTracks(playlistId, trackId, position, listened, creatDate) VALUES (@playlistId, @trackId, 1, 0, @now)",
                new { playlistId, trackId, now });
        }

        public Snapshot TakeSnapshot()
        {
            Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables = [];

            foreach (string table in SnapshotTables)
            {
                Dictionary<long, Dictionary<string, string>> rows = [];

                foreach (IDictionary<string, object?> row in _fixture.Connection.Query($"SELECT * FROM {table}").Cast<IDictionary<string, object?>>())
                {
                    rows[Convert.ToInt64(row["id"], CultureInfo.InvariantCulture)] = row.ToDictionary(
                        c => c.Key,
                        c => Convert.ToString(c.Value, CultureInfo.InvariantCulture) ?? "<null>");
                }

                tables[table] = rows;
            }

            return new Snapshot(tables);
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