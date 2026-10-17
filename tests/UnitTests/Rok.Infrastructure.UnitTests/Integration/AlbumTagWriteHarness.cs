using System.Globalization;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Messaging;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Compilation;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.Files;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Tag values of an audio file as the library import reads them.
/// </summary>
internal sealed record FileTags(string Album, string Artist, string Genre, int? Year);

/// <summary>
/// Database tables copied at one moment, to compare the state before and after a rescan.
/// </summary>
internal sealed class WriteSnapshot(Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables)
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

/// <summary>
/// Tag writer that refuses the writes of one file and delegates every other one to the real writer.
/// </summary>
internal sealed class FailingTagWriter(ITagWriter inner) : ITagWriter
{
    public string? FailingFileName { get; set; }

    public Task<IReadOnlyList<TagWriteCheck>> CheckAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        return inner.CheckAsync(paths, cancellationToken);
    }

    public async Task<IReadOnlyList<TagWriteResult>> WriteAsync(IReadOnlyList<TagWriteRequest> requests, CancellationToken cancellationToken)
    {
        List<TagWriteResult> results = [];

        foreach (TagWriteRequest request in requests)
        {
            if (FailingFileName is not null && Path.GetFileNameWithoutExtension(request.FilePath) == FailingFileName)
            {
                results.Add(new TagWriteResult(request.FilePath, ETagWriteOutcome.Failed, ETagWriteReason.IoError, null, null));
                continue;
            }

            results.AddRange(await inner.WriteAsync([request], cancellationToken));
        }

        return results;
    }
}

/// <summary>
/// Real components of the album edit and the library import over a dedicated SQLite database and real audio files:
/// repositories, <see cref="TagLibTagWriter"/> behind a failing writer, write gate, <see cref="TrackFileTagWriter"/>,
/// <see cref="AlbumEditService"/> called by its request handlers, and the complete <see cref="ImportService"/>.
/// </summary>
internal sealed class AlbumTagWriteHarness : IDisposable
{
    public const string AlbumName = "Old Album";
    public const string OldArtist = "Old Artist";
    public const string ExistingArtist = "Existing Artist";
    public const string OldGenre = "Rock";
    public const string OwnGenre = "Jazz";
    public const int OldYear = 1999;

    public const string FirstFile = "a1-track";
    public const string SecondFile = "a2-track";
    public const string OwnGenreFile = "a3-track";
    public const string NeighbourFile = "z-neighbour";
    public const string ExistingFile = "e1-track";
    public const string OtherAlbumFile = "o1-track";

    public static readonly string[] AlbumFiles = [FirstFile, SecondFile, OwnGenreFile];

    private static readonly string[] SnapshotTables =
    [
        "Tracks", "Albums", "Artists", "Genres", "ListeningEvents", "AlbumTags", "Tags", "EqualizerPreset", "PendingTagWrites"
    ];

    private readonly SqliteDatabaseFixture _fixture = new();
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("AlbumTagWriteHarness_");
    private readonly TrackRepository _trackRepository;
    private readonly AlbumRepository _albumRepository;
    private readonly ArtistRepository _artistRepository;
    private readonly GenreRepository _genreRepository;
    private readonly PendingTagWriteRepository _pendingRepository;
    private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
    private readonly FailingTagWriter _tagWriter;
    private readonly LibraryWriteGate _writeGate = new();
    private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true, ImportTrackWithArtistGenre = false };
    private readonly Dictionary<string, string> _paths = [];
    private readonly Func<string, string, string, string[], string[], string, uint, string> _createFile;
    private readonly AlbumEditService _service;
    private readonly AlbumMergeService _mergeService;
    private readonly AlbumCompilationService _compilationService;

    /// <summary>
    /// Creates the harness and the library: an album of three tracks (the third one has its own genre), a neighbour in
    /// the same folder, an existing artist with an album, and optionally a second album of the old artist.
    /// </summary>
    /// <param name="createFile">Audio file factory: folder, name, album, album artists, performers, genre, year.</param>
    /// <param name="oldArtistHasOtherAlbum">False makes the old artist lose its only album when the album moves.</param>
    /// <param name="importTrackWithArtistGenre">Value of the import option that gives each track the genre of its artist.</param>
    public AlbumTagWriteHarness(Func<string, string, string, string[], string[], string, uint, string> createFile, bool oldArtistHasOtherAlbum = true, bool importTrackWithArtistGenre = false)
    {
        _createFile = createFile;
        _options.ImportTrackWithArtistGenre = importTrackWithArtistGenre;

        _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

        _options.SetCachePath(Path.Combine(_root.FullName, "cache"));

        _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
        _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
        _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
        _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
        _pendingRepository = new PendingTagWriteRepository(_fixture.Connection, NullLogger<PendingTagWriteRepository>.Instance);
        _tagWriter = new FailingTagWriter(new TagLibTagWriter(Path.Combine(_root.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance));

        Mock<IPlayerService> player = new();
        Mock<IImport> import = new();
        import.SetupGet(i => i.UpdateInProgress).Returns(false);

        TrackFileTagWriter fileWriter = new(_tagWriter, player.Object, NullLogger<TrackFileTagWriter>.Instance);

        _mergeService = new AlbumMergeService(
            _albumRepository,
            _artistRepository,
            _trackRepository,
            new AlbumMergeRepository(_fixture.Connection, _pendingRepository, TimeProvider.System, NullLogger<AlbumMergeRepository>.Instance),
            _pendingRepository,
            fileWriter,
            import.Object,
            _writeGate,
            _options,
            NullLogger<AlbumMergeService>.Instance);

        _service = new AlbumEditService(
            _albumRepository,
            _artistRepository,
            _genreRepository,
            _trackRepository,
            new AlbumEditRepository(_fixture.Connection, _pendingRepository, TimeProvider.System, NullLogger<AlbumEditRepository>.Instance),
            _pendingRepository,
            fileWriter,
            import.Object,
            _writeGate,
            _options,
            NullLogger<AlbumEditService>.Instance);

        _compilationService = new AlbumCompilationService(
            _albumRepository,
            _trackRepository,
            new AlbumCompilationRepository(_fixture.Connection, _pendingRepository, TimeProvider.System, NullLogger<AlbumCompilationRepository>.Instance),
            _pendingRepository,
            fileWriter,
            import.Object,
            _writeGate,
            _options,
            NullLogger<AlbumCompilationService>.Instance);

        AddFile("album", FirstFile, AlbumName, OldArtist, OldGenre, OldYear);
        AddFile("album", SecondFile, AlbumName, OldArtist, OldGenre, OldYear);
        AddFile("album", OwnGenreFile, AlbumName, OldArtist, OwnGenre, OldYear);
        AddFile("album", NeighbourFile, "Neighbour album", "Neighbour artist", OldGenre, 1990);
        AddFile("existing", ExistingFile, "Existing Album", ExistingArtist, OldGenre, 2001);

        if (oldArtistHasOtherAlbum)
            AddFile("other", OtherAlbumFile, "Old Other Album", OldArtist, OldGenre, 1985);
    }

    private string LibraryFolder => Path.Combine(_root.FullName, "library");

    /// <summary>
    /// Creates a FLAC with the given tags.
    /// </summary>
    public static string CreateFlac(string folder, string name, string album, string[] albumArtists, string[] performers, string genre, uint year)
    {
        string path = FlacTagFixture.CreateWithArtists(folder, albumArtists, performers, name + ".flac");

        PrepareTags(path, name, album, genre, year);

        return path;
    }

    /// <summary>
    /// Creates an MP3 (ID3v2.3) with the given tags.
    /// </summary>
    public static string CreateMp3(string folder, string name, string album, string[] albumArtists, string[] performers, string genre, uint year)
    {
        string path = Mp3TagFixture.CreateWithArtists(folder, 3, albumArtists, performers, fileName: name + ".mp3");

        PrepareTags(path, name, album, genre, year);

        return path;
    }

    public T Scalar<T>(string sql, object? args = null)
    {
        return _fixture.Connection.ExecuteScalar<T>(sql, args)!;
    }

    public void AddFile(string folderName, string name, string album, string artist, string genre, int year, uint disc = 0, uint trackNumber = 0)
    {
        _paths[name] = _createFile(Path.Combine(LibraryFolder, folderName), name, album, [artist], ["Guest performer"], genre, (uint)year);

        if (disc != 0 || trackNumber != 0)
            SetNumbers(name, disc, trackNumber);
    }

    /// <summary>
    /// Writes the disc and track number of a file, before the library import.
    /// </summary>
    public void SetNumbers(string name, uint disc, uint trackNumber)
    {
        using TagLib.File file = TagLib.File.Create(_paths[name]);

        file.Tag.Disc = disc;
        file.Tag.Track = trackNumber;
        file.Save();
    }

    public uint ReadDisc(string name)
    {
        using TagLib.File file = TagLib.File.Create(_paths[name]);

        return file.Tag.Disc;
    }

    public long TrackId(string name)
    {
        return Scalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}%" });
    }

    public long AlbumIdOf(string name)
    {
        return Scalar<long>("SELECT albumId FROM Tracks WHERE id = @id", new { id = TrackId(name) });
    }

    public long ArtistId(string name)
    {
        return Scalar<long>("SELECT id FROM Artists WHERE name = @name COLLATE NOCASE", new { name });
    }

    public long GenreId(string name)
    {
        return Scalar<long>("SELECT id FROM Genres WHERE name = @name COLLATE NOCASE", new { name });
    }

    public int PendingCount()
    {
        return Scalar<int>("SELECT COUNT(*) FROM PendingTagWrites");
    }

    public FileTags ReadFile(string name)
    {
        using TagLib.File file = TagLib.File.Create(_paths[name]);

        return new FileTags(TagFieldReader.ReadAlbum(file.Tag), TagFieldReader.ReadArtist(file.Tag), TagFieldReader.ReadGenre(file.Tag), TagFieldReader.ReadYear(file.Tag));
    }

    /// <summary>
    /// Gives the album favorite, listens, a user tag, an equalizer preset and one listening event per track.
    /// </summary>
    public void SeedAlbumData(long albumId)
    {
        SeedAlbumData(albumId, AlbumFiles, "Album tag", true, 7, 6.5);
    }

    /// <summary>
    /// Gives the album the given favorite flag, listen count, user tag, equalizer preset (band 0) and one listening event per given track.
    /// </summary>
    public void SeedAlbumData(long albumId, IReadOnlyList<string> files, string tagName, bool favorite, int listenCount, double presetBand)
    {
        DateTime now = DateTime.UtcNow;
        long tagId = _fixture.Connection.ExecuteScalar<long>("INSERT INTO Tags(name) VALUES (@tagName); SELECT last_insert_rowid();", new { tagName });

        _fixture.Connection.Execute(
            "UPDATE Albums SET isFavorite = @favorite, listenCount = @listenCount, lastListen = @now WHERE id = @albumId",
            new { albumId, favorite, listenCount, now = now.AddDays(-1) });
        _fixture.Connection.Execute("INSERT INTO AlbumTags(albumId, tagId) VALUES (@albumId, @tagId)", new { albumId, tagId });
        _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Album', @albumId, @presetBand, 0)", new { albumId, presetBand });

        int minute = 0;

        foreach (string file in files)
        {
            long trackId = TrackId(file);

            _fixture.Connection.Execute(
                "INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal) SELECT id, artistId, albumId, genreId, @playedAt, 0, 120, 180 FROM Tracks WHERE id = @trackId",
                new { trackId, playedAt = now.AddMinutes(-minute++) });
        }
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

    /// <summary>
    /// Edits the album through <see cref="EditAlbumTagsRequestHandler"/>, the production caller of the service.
    /// </summary>
    public async Task<AlbumTagWriteOutcome> EditAsync(long albumId, string name, string artistName, int? year, string? genreName)
    {
        EditAlbumTagsRequestHandler handler = new(_service);

        Result<AlbumTagWriteOutcome> result = await handler.Handle(new EditAlbumTagsRequest(albumId, name, artistName, year, genreName), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    /// <summary>
    /// Edits the album through <see cref="EditAlbumTagsRequestHandler"/> and returns the raw result, so a refusal can be checked.
    /// </summary>
    public Task<Result<AlbumTagWriteOutcome>> TryEditAsync(long albumId, string name, string artistName, int? year, string? genreName)
    {
        EditAlbumTagsRequestHandler handler = new(_service);

        return handler.Handle(new EditAlbumTagsRequest(albumId, name, artistName, year, genreName), CancellationToken.None);
    }

    /// <summary>
    /// Writes again the pending files through <see cref="ResumeAlbumTagWriteRequestHandler"/>.
    /// </summary>
    public async Task<AlbumTagWriteOutcome> ResumeAsync(long albumId)
    {
        ResumeAlbumTagWriteRequestHandler handler = new(_service);

        Result<AlbumTagWriteOutcome> result = await handler.Handle(new ResumeAlbumTagWriteRequest(albumId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    /// <summary>
    /// Merges the albums through <see cref="MergeAlbumsRequestHandler"/>, the production caller of the service.
    /// </summary>
    public Task<Result<AlbumTagWriteOutcome>> MergeAsync(long sourceId, long targetId, bool shiftDiscs)
    {
        MergeAlbumsRequestHandler handler = new(_mergeService);

        return handler.Handle(new MergeAlbumsRequest(sourceId, targetId, shiftDiscs), CancellationToken.None);
    }

    /// <summary>
    /// Previews the merge through <see cref="GetAlbumMergePreviewRequestHandler"/>.
    /// </summary>
    public async Task<AlbumMergePreview> PreviewMergeAsync(long sourceId, long targetId)
    {
        GetAlbumMergePreviewRequestHandler handler = new(_mergeService);

        Result<AlbumMergePreview> result = await handler.Handle(new GetAlbumMergePreviewRequest(sourceId, targetId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    /// <summary>
    /// Sets the compilation status through <see cref="SetAlbumCompilationRequestHandler"/>, the production caller of the service.
    /// </summary>
    public async Task<AlbumTagWriteOutcome> SetCompilationAsync(long albumId, bool isCompilation)
    {
        SetAlbumCompilationRequestHandler handler = new(_compilationService);

        Result<AlbumTagWriteOutcome> result = await handler.Handle(new SetAlbumCompilationRequest(albumId, isCompilation), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    /// <summary>
    /// Writes the compilation flag of a file directly, as another tagger would.
    /// </summary>
    public void WriteCompilationFlag(string name, bool flag)
    {
        using TagLib.File file = TagLib.File.Create(_paths[name]);

        TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

        xiph.IsCompilation = flag;

        file.Save();
    }

    public bool ReadCompilationFlag(string name)
    {
        using TagLib.File file = TagLib.File.Create(_paths[name]);

        return file.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiph && xiph.IsCompilation;
    }

    public void SetWriteTagsEnabled(bool enabled)
    {
        _options.WriteTagsEnabled = enabled;
    }

    public void FailWritesTo(string name)
    {
        _tagWriter.FailingFileName = name;
    }

    public void StopFailingWrites()
    {
        _tagWriter.FailingFileName = null;
    }

    public void Touch(string name, int days)
    {
        File.SetLastWriteTimeUtc(_paths[name], DateTime.UtcNow.AddDays(days));
    }

    public void Retitle(string name, string title, int days)
    {
        using (TagLib.File file = TagLib.File.Create(_paths[name]))
        {
            file.Tag.Title = title;
            file.Save();
        }

        Touch(name, days);
    }

    public WriteSnapshot TakeSnapshot()
    {
        Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables = [];

        foreach (string table in SnapshotTables)
        {
            Dictionary<long, Dictionary<string, string>> rows = [];
            long index = 0;

            foreach (IDictionary<string, object?> row in _fixture.Connection.Query($"SELECT * FROM {table} ORDER BY 1, 2").Cast<IDictionary<string, object?>>())
            {
                Dictionary<string, string> values = row.ToDictionary(
                    c => c.Key,
                    c => Convert.ToString(c.Value, CultureInfo.InvariantCulture) ?? "<null>");

                string? idKey = row.Keys.FirstOrDefault(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase));

                rows[idKey is null ? index++ : Convert.ToInt64(row[idKey], CultureInfo.InvariantCulture)] = values;
            }

            tables[table] = rows;
        }

        return new WriteSnapshot(tables);
    }

    /// <summary>
    /// Checks that the stored counters of every artist and genre equal what the tracks and albums say.
    /// </summary>
    public void AssertCountersMatchTracks()
    {
        foreach (dynamic artist in _fixture.Connection.Query("SELECT id, name, trackCount, albumCount FROM Artists"))
        {
            long id = artist.id;

            Assert.True(
                Scalar<int>("SELECT COUNT(*) FROM Tracks WHERE artistId = @id", new { id }) == (int)artist.trackCount,
                $"trackCount of artist {artist.name}");
            Assert.True(
                Scalar<int>("SELECT COUNT(*) FROM Albums WHERE artistId = @id AND isCompilation = 0 AND isLive = 0 AND isBestOf = 0", new { id }) == (int)artist.albumCount,
                $"albumCount of artist {artist.name}");
        }

        foreach (dynamic genre in _fixture.Connection.Query("SELECT id, name, trackCount FROM Genres"))
        {
            long id = genre.id;

            Assert.True(
                Scalar<int>("SELECT COUNT(*) FROM Tracks WHERE genreId = @id", new { id }) == (int)genre.trackCount,
                $"trackCount of genre {genre.name}");
        }
    }

    /// <summary>
    /// Checks that two snapshots are equal, except the rows of the given tracks.
    /// </summary>
    public static void AssertSameExceptTouched(WriteSnapshot expectedSnapshot, WriteSnapshot actualSnapshot, params long[] touchedIds)
    {
        AssertSameExceptTouched(expectedSnapshot, actualSnapshot, [], touchedIds);
    }

    /// <summary>
    /// Checks that two snapshots are equal, except the rows of the given tracks and the tables to ignore.
    /// </summary>
    public static void AssertSameExceptTouched(WriteSnapshot expectedSnapshot, WriteSnapshot actualSnapshot, string[] ignoredTables, long[] touchedIds)
    {
        foreach (string table in SnapshotTables)
        {
            if (ignoredTables.Contains(table))
                continue;

            Dictionary<long, string> expected = expectedSnapshot.Rows(table);
            Dictionary<long, string> actual = actualSnapshot.Rows(table);

            if (table == "Tracks")
            {
                foreach (long touchedId in touchedIds)
                {
                    expected.Remove(touchedId);
                    actual.Remove(touchedId);
                }
            }

            Assert.Equal(expected, actual);
        }
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

    private static void PrepareTags(string path, string name, string album, string genre, uint year)
    {
        using TagLib.File file = TagLib.File.Create(path);

        file.Tag.Title = "Title " + name;
        file.Tag.Album = album;
        file.Tag.Genres = [genre];
        file.Tag.Year = year;

        file.Save();
    }

    private ImportService CreateImportService(IFolderResolver folderResolver)
    {
        TimeProvider time = TimeProvider.System;
        Mock<IMessenger> messenger = new();
        Mock<CleanArch.DevKit.Mediator.IMediator> mediator = new();
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