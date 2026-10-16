using System.Data;
using System.Globalization;
using System.Text.Json;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Messaging;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Playlists.Requests;
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
using Rok.Shared.Enums;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Renames an artist through <see cref="ArtistRenameService"/>, then runs the real library import again and checks that the rescan leaves the database as the rename left it.
/// </summary>
public sealed class ArtistRenameRescanTests
{
    private const string OldName = "Rescan Old Artist";
    private const string NewName = "Rescan New Artist";
    private const string CaseOldName = "rescan case artist";
    private const string CaseNewName = "Rescan Case Artist";
    private const string AlbumArtistFile = "track-a1";
    private const string PerformerFile = "track-a2";
    private const string NeighbourFile = "track-b";
    private const string LastNeighbourFile = "track-z-neighbour";
    private const string NeighbourRetitled = "Neighbour retitled by another reader";
    private const string NewFirstFile = "track-0-new-first";
    private const string RetitledFirst = "First file retitled by another reader";
    private const string RatingScore = "4";

    private static readonly string[] SnapshotTables =
    [
        "Tracks", "Albums", "Artists", "Genres", "Playlists", "PlaylistTracks", "ListeningEvents", "ArtistTags", "Tags", "EqualizerPreset"
    ];

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_rename_keeps_one_artist_with_the_same_id_mp3")]
    public async Task Rescan_AfterRenameMp3_ShouldKeepOneArtistWithTheSameId()
    {
        using Harness harness = new(CreateMp3, OldName, NewName);

        await AssertRescanChangesNothingAsync(harness);
    }

    [Fact(DisplayName = "rescan_after_rename_keeps_one_artist_with_the_same_id_flac")]
    public async Task Rescan_AfterRenameFlac_ShouldKeepOneArtistWithTheSameId()
    {
        using Harness harness = new(CreateFlac, OldName, NewName);

        await AssertRescanChangesNothingAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "rescan_after_rename_keeps_one_artist_with_the_same_id_m4a")]
    public async Task Rescan_AfterRenameM4a_ShouldKeepOneArtistWithTheSameId()
    {
        using Harness harness = new(CreateM4a, OldName, NewName);

        await AssertRescanChangesNothingAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_rename_keeps_favorite_listens_tags_and_equalizer_preset")]
    public async Task Rescan_AfterRename_ShouldKeepFavoriteListensTagsAndEqualizerPreset()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        harness.SeedArtistData(artistId);
        Snapshot seeded = harness.TakeSnapshot();

        // Act
        await harness.RenameAsync(artistId, NewName);
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal("1", after.Value("Artists", artistId, "isFavorite"));
        Assert.Equal(NewName, after.Value("Artists", artistId, "name"));
        Assert.Equal(seeded.Rows("ListeningEvents"), after.Rows("ListeningEvents"));
        Assert.Equal(2, after.Rows("ListeningEvents").Count);
        Assert.Equal(seeded.Rows("ArtistTags"), after.Rows("ArtistTags"));
        Assert.Single(after.Rows("ArtistTags"));
        Assert.Equal(seeded.Rows("Tags"), after.Rows("Tags"));
        Assert.Equal(seeded.Rows("EqualizerPreset"), after.Rows("EqualizerPreset"));
        Assert.Single(after.Rows("EqualizerPreset"));
        Assert.Equal(RatingScore, after.Value("Tracks", harness.TrackId(AlbumArtistFile), "score"));
        Assert.Equal(RatingScore, after.Value("Tracks", harness.TrackId(PerformerFile), "score"));
        Assert.Equal(artistId.ToString(CultureInfo.InvariantCulture), after.Value("EqualizerPreset", after.Rows("EqualizerPreset").Keys.Single(), "scopeId"));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_with_the_first_file_changed_keeps_every_other_track")]
    public async Task Rescan_WithTheFirstFileChanged_ShouldKeepEveryOtherTrack()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        harness.SeedArtistData(artistId);
        string first = harness.EnumeratedFiles()[0];
        long firstId = harness.TrackId(first);
        Snapshot before = harness.TakeSnapshot();

        // Act
        harness.Retitle(first, RetitledFirst, 2);
        string firstAfterTouch = harness.EnumeratedFiles()[0];
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(first, firstAfterTouch);
        Assert.Equal(1, statistics.TracksUpdated);
        Assert.Equal(0, statistics.TracksImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.ArtistsDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(3, after.Rows("Tracks").Count);
        Assert.True(harness.TrackExists(AlbumArtistFile));
        Assert.True(harness.TrackExists(PerformerFile));
        Assert.True(harness.TrackExists(NeighbourFile));
        Assert.Equal(RetitledFirst, after.Value("Tracks", firstId, "title"));
        Assert.Equal(RatingScore, after.Value("Tracks", firstId, "score"));
        Assert.Equal("1", after.Value("Artists", artistId, "isFavorite"));
        Assert.Equal(before.Rows("Artists").Count, after.Rows("Artists").Count);
        Assert.Equal(2, after.Rows("ListeningEvents").Count);
        AssertSameExceptTouched(before, after, firstId);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_with_a_new_file_first_keeps_every_other_track")]
    public async Task Rescan_WithANewFileFirst_ShouldKeepEveryOtherTrack()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        harness.SeedArtistData(artistId);
        Snapshot before = harness.TakeSnapshot();

        // Act
        harness.AddNewFile(NewFirstFile);
        string first = harness.EnumeratedFiles()[0];
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(NewFirstFile, first);
        Assert.Equal(1, statistics.TracksImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.ArtistsDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(4, after.Rows("Tracks").Count);
        Assert.True(harness.TrackExists(AlbumArtistFile));
        Assert.True(harness.TrackExists(PerformerFile));
        Assert.True(harness.TrackExists(NeighbourFile));
        Assert.True(harness.TrackExists(NewFirstFile));
        Assert.Equal("1", after.Value("Artists", artistId, "isFavorite"));
        Assert.Equal(before.Rows("Artists").Count, after.Rows("Artists").Count);
        Assert.Equal(RatingScore, after.Value("Tracks", harness.TrackId(AlbumArtistFile), "score"));

        foreach (string table in new[] { "ListeningEvents", "ArtistTags", "Tags", "EqualizerPreset", "Playlists", "PlaylistTracks" })
        {
            Assert.Equal(before.Rows(table), after.Rows(table));
        }

        Dictionary<long, string> tracksAfter = after.Rows("Tracks");

        tracksAfter.Remove(harness.TrackId(NewFirstFile));

        Assert.Equal(before.Rows("Tracks"), tracksAfter);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "case_only_rename_updates_tags_and_survives_the_rescan")]
    public async Task Rescan_AfterCaseOnlyRename_ShouldWriteTheTagsAndKeepTheArtist()
    {
        // Arrange
        using Harness harness = new(CreateMp3, CaseOldName, CaseNewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(CaseOldName);
        harness.SeedImages(CaseOldName);
        Snapshot before = harness.TakeSnapshot();

        // Act
        ArtistRenameOutcome outcome = await harness.RenameAsync(artistId, CaseNewName);
        Snapshot renamed = harness.TakeSnapshot();
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(CaseNewName, after.Value("Artists", artistId, "name"));
        Assert.Equal(CaseNewName, harness.ReadArtistFromFile(AlbumArtistFile));
        Assert.Equal(CaseNewName, harness.ReadArtistFromFile(PerformerFile));
        Assert.Equal(before.Rows("Artists").Count, after.Rows("Artists").Count);
        Assert.Equal(artistId, harness.ArtistId(CaseNewName));
        Assert.Equal(CaseNewName.ToFileName(), harness.ArtistFolderNameOnDisk(CaseNewName));
        Assert.True(harness.ImagesExist(CaseNewName));
        Assert.Equal(1, statistics.TracksUpdated);
        Assert.Equal(0, statistics.ArtistsImported);
        Assert.Equal(0, statistics.ArtistsDeleted);
        AssertSameExceptTouched(renamed, after, harness.TrackId(NeighbourFile));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_a_partial_rename_never_recreates_the_artist")]
    public async Task Rescan_AfterPartialRename_ShouldNeverRecreateTheArtist()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName, LastNeighbourFile);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        int artistCount = harness.TakeSnapshot().Rows("Artists").Count;
        harness.FailWritesTo(PerformerFile);

        // Act
        ArtistRenameOutcome partial = await harness.RenameAsync(artistId, NewName);
        Snapshot afterIncident = harness.TakeSnapshot();
        harness.Touch(PerformerFile, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        Snapshot afterRescan = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.State);
        Assert.Equal(PerformerFile, Path.GetFileNameWithoutExtension(Assert.Single(partial.Failures).FilePath));
        Assert.Equal(OldName, afterIncident.Value("Artists", artistId, "renamePendingFrom"));
        Assert.Equal(NewName, harness.ReadArtistFromFile(AlbumArtistFile));
        Assert.Equal(OldName, harness.ReadArtistFromFile(PerformerFile));

        Assert.Equal(1, rescan.TracksUpdated);
        Assert.Equal(0, rescan.TracksDeleted);
        Assert.Equal(0, rescan.ArtistsImported);
        Assert.Equal(0, rescan.ArtistsDeleted);
        Assert.True(harness.TrackExists(LastNeighbourFile));
        Assert.Equal(artistCount, afterRescan.Rows("Artists").Count);
        Assert.Equal(artistId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", harness.TrackId(AlbumArtistFile), "artistId"));
        Assert.Equal(artistId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", harness.TrackId(PerformerFile), "artistId"));
        Assert.Equal(NewName, afterRescan.Value("Artists", artistId, "name"));
        Assert.Equal(OldName, afterRescan.Value("Artists", artistId, "renamePendingFrom"));

        // Act: resume, then rescan again
        harness.StopFailingWrites();
        ArtistRenameOutcome resumed = await harness.RenameAsync(artistId, NewName);
        Snapshot afterResume = harness.TakeSnapshot();
        harness.Retitle(PerformerFile, "Retitled by another reader", 4);
        ImportStatisticsDto finalRescan = await harness.ImportAsync();
        Snapshot final = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, resumed.State);
        Assert.Equal(string.Empty, afterResume.Value("Artists", artistId, "renamePendingFrom"));
        Assert.Equal(NewName, harness.ReadArtistFromFile(PerformerFile));
        Assert.Equal(1, finalRescan.TracksUpdated);
        Assert.Equal("Retitled by another reader", final.Value("Tracks", harness.TrackId(PerformerFile), "title"));
        Assert.Equal(0, finalRescan.TracksDeleted);
        Assert.Equal(0, finalRescan.ArtistsImported);
        Assert.Equal(0, finalRescan.ArtistsDeleted);
        Assert.True(harness.TrackExists(LastNeighbourFile));
        AssertSameExceptTouched(afterResume, final, harness.TrackId(PerformerFile));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "smart_playlists_return_the_same_tracks_after_rename")]
    public async Task SmartPlaylists_AfterRenameAndRescan_ShouldReturnTheSameTracks()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        harness.SeedSmartPlaylists();
        Dictionary<string, long[]> before = await harness.GenerateSmartPlaylistsAsync();

        // Act
        await harness.RenameAsync(artistId, NewName);
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Dictionary<string, long[]> after = await harness.GenerateSmartPlaylistsAsync();

        // Assert
        long[] artistTracks = [harness.TrackId(AlbumArtistFile), harness.TrackId(PerformerFile)];

        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.Equal(artistTracks.Order(), after["Smart equals"]);
        Assert.Equal(artistTracks.Order(), after["Smart contains"]);
        Assert.Equal([harness.TrackId(NeighbourFile)], after["Smart not equals"]);

        foreach (string name in before.Keys)
        {
            Assert.Equal(before[name], after[name]);
            Assert.Contains(NewName, harness.CriteriaJson(name), StringComparison.Ordinal);
            Assert.DoesNotContain(OldName, harness.CriteriaJson(name), StringComparison.Ordinal);
        }
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "playlist_picture_and_artist_images_follow_the_rename")]
    public async Task Rename_ShouldMovePlaylistPictureAndArtistImages()
    {
        // Arrange
        using Harness harness = new(CreateMp3, OldName, NewName);
        await harness.ImportTwiceAsync();
        long artistId = harness.ArtistId(OldName);
        harness.SeedImages(OldName);
        long playlistId = harness.SeedClassicPlaylist(OldName);

        // Act
        ArtistRenameOutcome outcome = await harness.RenameAsync(artistId, NewName);
        harness.RetitleNeighbourAsAnotherReader();
        await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal([playlistId], outcome.ChangedPlaylistIds);
        Assert.Equal(NewName, after.Value("Playlists", playlistId, "picture"));
        Assert.True(harness.ImagesExist(NewName));
        Assert.False(harness.ImagesExist(OldName));
    }

    private static async Task AssertRescanChangesNothingAsync(Harness harness)
    {
        // Arrange
        await harness.ImportTwiceAsync();

        long artistId = harness.ArtistId(OldName);
        long neighbourId = harness.TrackId(NeighbourFile);
        harness.SeedArtistData(artistId);
        harness.SeedSmartPlaylists();
        int artistCount = harness.TakeSnapshot().Rows("Artists").Count;

        ArtistRenameOutcome outcome = await harness.RenameAsync(artistId, NewName);
        Snapshot before = harness.TakeSnapshot();

        // Act
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(NewName, before.Value("Artists", artistId, "name"));
        Assert.Equal(string.Empty, before.Value("Artists", artistId, "renamePendingFrom"));
        Assert.Equal(NewName, harness.ReadArtistFromFile(AlbumArtistFile));
        Assert.Equal(NewName, harness.ReadArtistFromFile(PerformerFile));

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

        Assert.Equal(artistCount, after.Rows("Artists").Count);
        Assert.Equal(artistId, harness.ArtistId(NewName));
        Assert.Equal(artistId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(AlbumArtistFile), "artistId"));
        Assert.Equal(artistId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(PerformerFile), "artistId"));
        Assert.True(harness.ImagesExist(NewName));

        AssertSameExceptTouched(before, after, neighbourId);
    }

    private static void AssertSameExceptTouched(Snapshot expectedSnapshot, Snapshot actualSnapshot, long touchedId)
    {
        foreach (string table in SnapshotTables)
        {
            Dictionary<long, string> expected = expectedSnapshot.Rows(table);
            Dictionary<long, string> actual = actualSnapshot.Rows(table);

            if (table == "Tracks")
            {
                expected.Remove(touchedId);
                actual.Remove(touchedId);
            }

            Assert.Equal(expected, actual);
        }
    }

    private static string CreateMp3(string folder, string name, string[] albumArtists, string[] performers)
    {
        string path = Mp3TagFixture.CreateWithArtists(folder, 3, albumArtists, performers, fileName: name + ".mp3");

        PrepareTags(path, name);

        return path;
    }

    private static string CreateFlac(string folder, string name, string[] albumArtists, string[] performers)
    {
        string path = FlacTagFixture.CreateWithArtists(folder, albumArtists, performers, name + ".flac");

        PrepareTags(path, name);

        return path;
    }

    private static string CreateM4a(string folder, string name, string[] albumArtists, string[] performers)
    {
        string path = M4aTagFixture.CreateWithArtists(folder, EMp4Layout.MoovBeforeMdat, albumArtists, performers, name + ".m4a");

        PrepareTags(path, name);

        return path;
    }

    private static void PrepareTags(string path, string name)
    {
        using TagLib.File file = TagLib.File.Create(path);

        file.Tag.Title = "Title " + name;
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

    private sealed class SmartPlaylistRow
    {
        public string Name { get; set; } = string.Empty;

        public string GroupsJson { get; set; } = string.Empty;
    }

    private sealed class FailingTagWriter(ITagWriter inner) : ITagWriter
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

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ArtistRenameRescanTests_");
        private readonly TrackRepository _trackRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistRepository _artistRepository;
        private readonly GenreRepository _genreRepository;
        private readonly PlaylistHeaderRepository _playlistHeaderRepository;
        private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
        private readonly FailingTagWriter _tagWriter;
        private readonly ArtistPicture _artistPicture;
        private readonly LibraryWriteGate _writeGate = new();
        private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true };
        private readonly Dictionary<string, string> _paths = [];
        private readonly string _oldName;
        private readonly Func<string, string, string[], string[], string> _createFile;

        public Harness(Func<string, string, string[], string[], string> createFile, string oldName, string newName, string neighbourName = NeighbourFile)
        {
            _oldName = oldName;
            _createFile = createFile;
            NeighbourName = neighbourName;
            NewName = newName;

            _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

            _options.SetCachePath(Path.Combine(_root.FullName, "cache"));

            _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
            _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
            _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
            _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
            _playlistHeaderRepository = new PlaylistHeaderRepository(_fixture.Connection, _fixture.Connection, NullLogger<PlaylistHeaderRepository>.Instance, TimeProvider.System);
            _tagWriter = new FailingTagWriter(new TagLibTagWriter(Path.Combine(_root.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance));
            _artistPicture = new ArtistPicture(new DefaultFileSystem(), _options);

            _paths[AlbumArtistFile] = createFile(LibraryFolder, AlbumArtistFile, [oldName], ["Guest performer"]);
            _paths[PerformerFile] = createFile(LibraryFolder, PerformerFile, [], [oldName]);
            _paths[neighbourName] = createFile(LibraryFolder, neighbourName, [], ["Neighbour artist"]);
        }

        public string NewName { get; }

        public string NeighbourName { get; }

        private string LibraryFolder => Path.Combine(_root.FullName, "library");

        public List<string> EnumeratedFiles()
        {
            FileSystemService fileSystem = new(NullLogger<FileSystemService>.Instance);

            return fileSystem.GetMusicFiles(LibraryFolder, _tagService.FillBasicProperties)
                .Select(f => Path.GetFileNameWithoutExtension(f.FullPath))
                .ToList();
        }

        public void AddNewFile(string name)
        {
            _paths[name] = _createFile(LibraryFolder, name, [], ["Neighbour artist"]);
        }

        public long TrackId(string name)
        {
            return _fixture.Connection.ExecuteScalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}%" });
        }

        public bool TrackExists(string name)
        {
            return _fixture.Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}%" }) == 1;
        }

        public long ArtistId(string name)
        {
            return _fixture.Connection.ExecuteScalar<long>("SELECT id FROM Artists WHERE name = @name", new { name });
        }

        public string ReadArtistFromFile(string name)
        {
            using TagLib.File file = TagLib.File.Create(_paths[name]);

            return TagFieldReader.ReadArtist(file.Tag);
        }

        public string? ArtistFolderNameOnDisk(string artistName)
        {
            string folder = _artistPicture.GetArtistFolder(artistName);
            string? parent = Path.GetDirectoryName(folder);

            return parent is null ? null : Directory.GetDirectories(parent).Select(Path.GetFileName).SingleOrDefault(n => string.Equals(n, Path.GetFileName(folder), StringComparison.OrdinalIgnoreCase));
        }

        public bool ImagesExist(string artistName)
        {
            string folder = _artistPicture.GetArtistFolder(artistName);

            return _artistPicture.PictureFileExists(artistName) && Directory.Exists(folder) && Directory.GetFiles(folder, "backdrop*").Length == 1;
        }

        public void SeedImages(string artistName)
        {
            Directory.CreateDirectory(_artistPicture.GetArtistFolder(artistName));

            File.WriteAllBytes(_artistPicture.GetPictureFile(artistName), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(_artistPicture.GetArtistFolder(artistName), "backdrop1.jpg"), [4, 5, 6]);
        }

        public void SeedArtistData(long artistId)
        {
            long trackId = TrackId(AlbumArtistFile);
            long albumId = _fixture.Connection.ExecuteScalar<long>("SELECT albumId FROM Tracks WHERE id = @trackId", new { trackId });
            DateTime now = DateTime.UtcNow;

            _fixture.Connection.Execute("UPDATE Artists SET isFavorite = 1 WHERE id = @artistId", new { artistId });
            _fixture.Connection.Execute("UPDATE Tracks SET score = @score", new { score = int.Parse(RatingScore, CultureInfo.InvariantCulture) });

            for (int i = 0; i < 2; i++)
            {
                _fixture.Connection.Execute(
                    "INSERT INTO ListeningEvents(trackId, artistId, albumId, playedAt, wasSkipped, durationPlayed, durationTotal) VALUES (@trackId, @artistId, @albumId, @playedAt, 0, 120, 180)",
                    new { trackId, artistId, albumId, playedAt = now.AddMinutes(-i) });
            }

            long tagId = _fixture.Connection.ExecuteScalar<long>("INSERT INTO Tags(name) VALUES ('Rescan tag'); SELECT last_insert_rowid();");

            _fixture.Connection.Execute("INSERT INTO ArtistTags(artistId, tagId) VALUES (@artistId, @tagId)", new { artistId, tagId });
            _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Artist', @artistId, 3.5, -2.5)", new { artistId });

            SeedImages(_oldName);
            SeedClassicPlaylist(_oldName);
        }

        public long SeedClassicPlaylist(string picture)
        {
            return InsertPlaylist("Rescan classic", picture, (int)PlaylistType.Classic, string.Empty);
        }

        public void SeedSmartPlaylists()
        {
            foreach ((string name, SmartPlaylistOperator op) in new[]
            {
                ("Smart equals", SmartPlaylistOperator.Equals),
                ("Smart not equals", SmartPlaylistOperator.NotEquals),
                ("Smart contains", SmartPlaylistOperator.Contains)
            })
            {
                List<PlaylistGroupDto> groups =
                [
                    new()
                    {
                        Name = "group",
                        Filters =
                        [
                            new()
                            {
                                Entity = SmartPlaylistEntity.Artists,
                                Field = SmartPlaylistField.Name,
                                FieldType = SmartPlaylistFieldType.String,
                                Operator = op,
                                Value = _oldName
                            }
                        ]
                    }
                ];

                InsertPlaylist(name, string.Empty, (int)PlaylistType.Smart, JsonSerializer.Serialize(groups));
            }
        }

        public string CriteriaJson(string playlistName)
        {
            return _fixture.Connection.ExecuteScalar<string>("SELECT groupsJson FROM Playlists WHERE name = @playlistName", new { playlistName }) ?? string.Empty;
        }

        public async Task<Dictionary<string, long[]>> GenerateSmartPlaylistsAsync()
        {
            PlaylistTrackGenerateRepository repository = new(_fixture.Connection, _fixture.Connection, NullLogger<PlaylistTrackGenerateRepository>.Instance, TimeProvider.System);
            Dictionary<string, long[]> result = [];

            foreach (SmartPlaylistRow row in _fixture.Connection.Query<SmartPlaylistRow>("SELECT name, groupsJson FROM Playlists WHERE type = 0"))
            {
                List<PlaylistGroupDto> groups = JsonSerializer.Deserialize<List<PlaylistGroupDto>>(row.GroupsJson)!;
                List<long> ids = [];

                foreach (PlaylistGroupDto group in groups)
                {
                    ids.AddRange((await repository.GenerateAsync(new GeneratePlaylistTracksRequest { PlaylistTrackCount = 1000, Group = group })).Select(t => t.Id));
                }

                result[row.Name] = ids.Order().ToArray();
            }

            return result;
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

        public async Task<ArtistRenameOutcome> RenameAsync(long artistId, string newName)
        {
            Mock<IPlayerService> player = new();
            Mock<IImport> import = new();
            import.SetupGet(i => i.UpdateInProgress).Returns(false);

            ArtistRenameService service = new(
                _artistRepository,
                _trackRepository,
                _playlistHeaderRepository,
                new ArtistRenameRepository(_fixture.Connection, NullLogger<ArtistRenameRepository>.Instance),
                _tagWriter,
                _artistPicture,
                player.Object,
                import.Object,
                _writeGate,
                _options,
                NullLogger<ArtistRenameService>.Instance);

            Result<ArtistRenameOutcome> result = await service.RenameAsync(artistId, newName, null, CancellationToken.None);

            Assert.True(result.IsSuccess);

            return result.Value;
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

        public void RetitleNeighbourAsAnotherReader()
        {
            Retitle(NeighbourName, NeighbourRetitled, 2);
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

        public Snapshot TakeSnapshot()
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

        private long InsertPlaylist(string name, string picture, int type, string groupsJson)
        {
            return _fixture.Connection.ExecuteScalar<long>(
                "INSERT INTO Playlists(name, picture, trackMaximum, durationMaximum, trackCount, duration, groupsJson, type, creatDate) VALUES (@name, @picture, 0, 0, 0, 0, @groupsJson, @type, @now); SELECT last_insert_rowid();",
                new { name, picture, groupsJson, type, now = DateTime.UtcNow });
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