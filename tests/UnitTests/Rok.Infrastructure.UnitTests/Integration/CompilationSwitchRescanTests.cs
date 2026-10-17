using System.Data;
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
using Rok.Application.Tag;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Runs the real library import on folders whose number of artists changes between two scans and checks that the album keeps its identity and its data.
/// </summary>
public sealed class CompilationSwitchRescanTests
{
    private const string GenreName = "Rock";

    [Fact(DisplayName = "rescan_keeps_the_album_when_a_folder_goes_from_two_artists_to_one")]
    public async Task Rescan_WhenFolderGoesFromTwoArtistsToOne_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Switch Album";
        const string kept = "Rescan Band";
        const string abandoned = "The Rescan Band";
        string[] files = ["sw1-a", "sw2-a", "sw3-b", "sw4-b"];

        using Harness harness = new();

        harness.AddFile("switch", files[0], kept, album);
        harness.AddFile("switch", files[1], kept, album);
        harness.AddFile("switch", files[2], abandoned, album);
        harness.AddFile("switch", files[3], abandoned, album);

        await harness.ImportTwiceAsync();

        long keptId = harness.ArtistId(kept);
        AlbumBaseline baseline = harness.SeedAlbumData(album, files);
        int genreAlbums = harness.Scalar<int>("SELECT albumCount FROM Genres WHERE name = @name", new { name = GenreName });
        int genreCompilations = harness.Scalar<int>("SELECT compilationCount FROM Genres WHERE name = @name", new { name = GenreName });

        // Act
        harness.Retag(files[2], kept, 2);
        harness.Retag(files[3], kept, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 2, "positive control: the retagged files must be processed again");
        Assert.Equal(kept, harness.ArtistNameOf(files[2]));
        Assert.Equal(kept, harness.ArtistNameOf(files[3]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(0, harness.IsCompilation(baseline.AlbumId));
        Assert.Equal(keptId, harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @id", new { id = baseline.AlbumId }));
        Assert.Equal(1, rescan.ArtistsDeleted);
        Assert.Equal(1, harness.Scalar<int>("SELECT albumCount FROM Artists WHERE id = @keptId", new { keptId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT compilationCount FROM Artists WHERE id = @keptId", new { keptId }));
        Assert.Equal(4, harness.Scalar<int>("SELECT trackCount FROM Artists WHERE id = @keptId", new { keptId }));
        Assert.Equal(genreAlbums + 1, harness.Scalar<int>("SELECT albumCount FROM Genres WHERE name = @name", new { name = GenreName }));
        Assert.Equal(genreCompilations - 1, harness.Scalar<int>("SELECT compilationCount FROM Genres WHERE name = @name", new { name = GenreName }));
    }

    [Fact(DisplayName = "rescan_of_every_file_keeps_the_album_when_a_folder_goes_from_two_artists_to_one")]
    public async Task Rescan_OfEveryFileWhenFolderGoesFromTwoArtistsToOne_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Switch All Album";
        const string kept = "Rescan All Band";
        const string abandoned = "The Rescan All Band";
        string[] files = ["all1-a", "all2-a", "all3-b", "all4-b"];

        using Harness harness = new();

        harness.AddFile("switchall", files[0], kept, album);
        harness.AddFile("switchall", files[1], kept, album);
        harness.AddFile("switchall", files[2], abandoned, album);
        harness.AddFile("switchall", files[3], abandoned, album);

        await harness.ImportTwiceAsync();

        long keptId = harness.ArtistId(kept);
        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.Retag(files[2], kept, 2);
        harness.Retag(files[3], kept, 2);
        harness.Retitle(files[0], "Retitled all1", 2);
        harness.Retitle(files[1], "Retitled all2", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 4, "positive control: every file must be processed again");
        Assert.Equal(kept, harness.ArtistNameOf(files[2]));
        Assert.Equal("Retitled all1", harness.TitleOf(files[0]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(0, harness.IsCompilation(baseline.AlbumId));
        Assert.Equal(keptId, harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @id", new { id = baseline.AlbumId }));
        Assert.Equal(1, rescan.ArtistsDeleted);
        Assert.Equal(1, harness.Scalar<int>("SELECT albumCount FROM Artists WHERE id = @keptId", new { keptId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT compilationCount FROM Artists WHERE id = @keptId", new { keptId }));
        Assert.Equal(4, harness.Scalar<int>("SELECT trackCount FROM Artists WHERE id = @keptId", new { keptId }));
    }

    [Fact(DisplayName = "rescan_keeps_the_album_when_a_folder_goes_from_one_artist_to_two")]
    public async Task Rescan_WhenFolderGoesFromOneArtistToTwo_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Split Album";
        const string owner = "Split Owner";
        const string guest = "Split Guest";
        string[] files = ["sp1-a", "sp2-a", "sp3-a"];

        using Harness harness = new();

        foreach (string file in files)
        {
            harness.AddFile("split", file, owner, album);
        }

        await harness.ImportTwiceAsync();

        long ownerId = harness.ArtistId(owner);
        AlbumBaseline baseline = harness.SeedAlbumData(album, files);
        int genreAlbums = harness.Scalar<int>("SELECT albumCount FROM Genres WHERE name = @name", new { name = GenreName });
        int genreCompilations = harness.Scalar<int>("SELECT compilationCount FROM Genres WHERE name = @name", new { name = GenreName });

        // Act
        harness.Retag(files[2], guest, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal(guest, harness.ArtistNameOf(files[2]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
        Assert.Equal(ownerId, harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @id", new { id = baseline.AlbumId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT albumCount FROM Artists WHERE id = @ownerId", new { ownerId }));
        Assert.Equal(1, harness.Scalar<int>("SELECT compilationCount FROM Artists WHERE id = @ownerId", new { ownerId }));
        Assert.Equal(genreAlbums - 1, harness.Scalar<int>("SELECT albumCount FROM Genres WHERE name = @name", new { name = GenreName }));
        Assert.Equal(genreCompilations + 1, harness.Scalar<int>("SELECT compilationCount FROM Genres WHERE name = @name", new { name = GenreName }));
    }

    [Fact(DisplayName = "rescan_keeps_the_album_when_a_new_file_of_another_artist_joins_the_folder")]
    public async Task Rescan_WhenANewFileOfAnotherArtistJoinsTheFolder_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Joined Album";
        string[] files = ["jn1-a", "jn2-a"];
        const string newcomer = "jn3-b";

        using Harness harness = new();

        foreach (string file in files)
        {
            harness.AddFile("joined", file, "Joined Owner", album);
        }

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.AddFile("joined", newcomer, "Joined Guest", album);
        harness.Touch(newcomer, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.Equal(1, rescan.TracksImported);
        Assert.Equal("Joined Guest", harness.ArtistNameOf(newcomer));
        harness.AssertAlbumKept(rescan, baseline, [.. files, newcomer], expectedTrackCount: 3);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_keeps_the_album_when_both_disc_folders_go_from_two_artists_to_one")]
    public async Task Rescan_WhenBothDiscFoldersGoFromTwoArtistsToOne_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Two Discs Compilation";
        const string kept = "Disc Artist One";
        const string other = "Disc Artist Two";
        string[] files = ["d1-a", "d1-b", "d2-a", "d2-b"];

        using Harness harness = new();

        harness.AddFile(Path.Combine("discs", "cd1"), files[0], kept, album);
        harness.AddFile(Path.Combine("discs", "cd1"), files[1], other, album);
        harness.AddFile(Path.Combine("discs", "cd2"), files[2], kept, album);
        harness.AddFile(Path.Combine("discs", "cd2"), files[3], other, album);

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.Retag(files[1], kept, 2);
        harness.Retag(files[3], kept, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 2, "positive control: the retagged files must be processed again");
        Assert.Equal(kept, harness.ArtistNameOf(files[1]));
        Assert.Equal(kept, harness.ArtistNameOf(files[3]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(0, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_keeps_the_album_when_both_disc_folders_go_from_one_artist_to_two")]
    public async Task Rescan_WhenBothDiscFoldersGoFromOneArtistToTwo_ShouldKeepTheAlbum()
    {
        // Arrange
        const string album = "Two Discs Album";
        const string owner = "Disc Owner";
        const string guest = "Disc Guest";
        string[] files = ["e1-a", "e1-b", "e2-a", "e2-b"];

        using Harness harness = new();

        harness.AddFile(Path.Combine("discsnormal", "cd1"), files[0], owner, album);
        harness.AddFile(Path.Combine("discsnormal", "cd1"), files[1], owner, album);
        harness.AddFile(Path.Combine("discsnormal", "cd2"), files[2], owner, album);
        harness.AddFile(Path.Combine("discsnormal", "cd2"), files[3], owner, album);

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.Retag(files[1], guest, 2);
        harness.Retag(files[3], guest, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 2, "positive control: the retagged files must be processed again");
        Assert.Equal(guest, harness.ArtistNameOf(files[1]));
        Assert.Equal(guest, harness.ArtistNameOf(files[3]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_creates_an_unknown_album_normally")]
    public async Task Rescan_WithAnUnknownAlbum_ShouldCreateIt()
    {
        // Arrange
        const string existing = "Existing Album";
        string[] existingFiles = ["ex1-a", "ex2-a"];
        string[] newFiles = ["nw1-a", "nw2-a", "nw3-a"];

        using Harness harness = new();

        foreach (string file in existingFiles)
        {
            harness.AddFile("existing", file, "Existing Owner", existing);
        }

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(existing, existingFiles);
        int tracksBefore = harness.Scalar<int>("SELECT COUNT(*) FROM Tracks");

        // Act
        foreach (string file in newFiles)
        {
            harness.AddFile("fresh", file, "Fresh Owner", "Fresh Album");
            harness.Touch(file, 2);
        }

        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.Equal(1, rescan.AlbumsImported);
        Assert.Equal(3, rescan.TracksImported);
        Assert.Equal(0, rescan.TracksDeleted);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(tracksBefore + 3, harness.Scalar<int>("SELECT COUNT(*) FROM Tracks"));
        Assert.Single(harness.AlbumIdsNamed("Fresh Album"));
        Assert.Equal(baseline.AlbumId, Assert.Single(harness.AlbumIdsNamed(existing)));

        foreach (string file in existingFiles)
        {
            Assert.Equal(baseline.AlbumId, harness.AlbumIdOf(file));
        }
    }

    [Fact(DisplayName = "rescan_keeps_two_homonym_albums_of_two_artists_in_two_folders")]
    public async Task Rescan_WithHomonymAlbumsInTwoFolders_ShouldKeepBoth()
    {
        // Arrange
        const string album = "Greatest Hits";
        string[] filesA = ["ha1", "ha2"];
        string[] filesB = ["hb1", "hb2"];

        using Harness harness = new();

        foreach (string file in filesA)
        {
            harness.AddFile("homonym-a", file, "Homonym Artist A", album);
        }

        foreach (string file in filesB)
        {
            harness.AddFile("homonym-b", file, "Homonym Artist B", album);
        }

        await harness.ImportTwiceAsync();

        long albumA = harness.AlbumIdOf(filesA[0]);
        long albumB = harness.AlbumIdOf(filesB[0]);
        long artistA = harness.ArtistId("Homonym Artist A");
        long artistB = harness.ArtistId("Homonym Artist B");

        // Act
        harness.Retitle(filesA[0], "Retitled ha1", 2);
        harness.Retitle(filesB[0], "Retitled hb1", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.Equal(2, rescan.TracksUpdated);
        Assert.Equal("Retitled ha1", harness.TitleOf(filesA[0]));
        Assert.Equal("Retitled hb1", harness.TitleOf(filesB[0]));
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(0, rescan.TracksDeleted);
        Assert.NotEqual(albumA, albumB);
        Assert.Equal(new[] { albumA, albumB }.Order(), harness.AlbumIdsNamed(album));
        Assert.Equal(albumA, harness.AlbumIdOf(filesA[1]));
        Assert.Equal(albumB, harness.AlbumIdOf(filesB[1]));
        Assert.Equal(artistA, harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @albumA", new { albumA }));
        Assert.Equal(artistB, harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @albumB", new { albumB }));
        Assert.Equal(0, harness.IsCompilation(albumA));
        Assert.Equal(0, harness.IsCompilation(albumB));
    }

    [Fact(DisplayName = "rescan_keeps_one_compilation_spread_over_two_multi_artist_folders")]
    public async Task Rescan_WithACompilationOverTwoMultiArtistFolders_ShouldKeepOneAlbum()
    {
        // Arrange
        const string album = "Spread Compilation";
        string[] files = ["sc1-a", "sc1-b", "sc2-c", "sc2-d"];

        using Harness harness = new();

        harness.AddFile(Path.Combine("spread", "cd1"), files[0], "Spread Artist A", album);
        harness.AddFile(Path.Combine("spread", "cd1"), files[1], "Spread Artist B", album);
        harness.AddFile(Path.Combine("spread", "cd2"), files[2], "Spread Artist C", album);
        harness.AddFile(Path.Combine("spread", "cd2"), files[3], "Spread Artist D", album);

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.Retitle(files[0], "Retitled sc1", 2);
        harness.Retitle(files[2], "Retitled sc2", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.Equal(2, rescan.TracksUpdated);
        Assert.Equal("Retitled sc1", harness.TitleOf(files[0]));
        Assert.Equal("Retitled sc2", harness.TitleOf(files[2]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_keeps_a_compilation_when_only_one_of_its_folders_drops_to_one_artist")]
    public async Task Rescan_WhenOnlyOneFolderDropsToOneArtist_ShouldKeepTheCompilation()
    {
        // Arrange
        const string album = "Half Switched Compilation";
        const string single = "Half Artist C";
        string[] files = ["hs1-a", "hs1-b", "hs2-c", "hs2-d"];

        using Harness harness = new();

        harness.AddFile(Path.Combine("half", "cd1"), files[0], "Half Artist A", album);
        harness.AddFile(Path.Combine("half", "cd1"), files[1], "Half Artist B", album);
        harness.AddFile(Path.Combine("half", "cd2"), files[2], single, album);
        harness.AddFile(Path.Combine("half", "cd2"), files[3], "Half Artist D", album);

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        harness.Retag(files[3], single, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal(single, harness.ArtistNameOf(files[3]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_keeps_a_compilation_flag_set_by_the_user")]
    public async Task Rescan_WhenTheUserFlaggedTheAlbumAsCompilation_ShouldKeepTheFlag()
    {
        // Arrange
        const string album = "Manual Compilation";
        string[] files = ["mc1-a", "mc2-a"];

        using Harness harness = new();

        foreach (string file in files)
        {
            harness.AddFile("manual", file, "Manual Owner", album);
        }

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);
        harness.Execute("UPDATE Albums SET isCompilation = 1 WHERE id = @albumId", new { albumId = baseline.AlbumId });

        // Act
        harness.Retitle(files[0], "Retitled mc1", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled file must be processed again");
        Assert.Equal("Retitled mc1", harness.TitleOf(files[0]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(1, harness.IsCompilation(baseline.AlbumId));
    }

    [Fact(DisplayName = "rescan_moves_the_album_to_the_new_artist_when_every_track_is_retagged")]
    public async Task Rescan_WhenEveryTrackMovesToAnotherArtist_ShouldMoveTheAlbum()
    {
        // Arrange
        const string album = "Moved Album";
        const string before = "Moved Band";
        const string after = "The Moved Band";
        string[] files = ["mv1-a", "mv2-a", "mv3-a"];

        using Harness harness = new();

        foreach (string file in files)
        {
            harness.AddFile("moved", file, before, album);
        }

        await harness.ImportTwiceAsync();

        AlbumBaseline baseline = harness.SeedAlbumData(album, files);

        // Act
        foreach (string file in files)
        {
            harness.Retag(file, after, 2);
        }

        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 3, "positive control: every retagged file must be processed again");
        Assert.Equal(after, harness.ArtistNameOf(files[0]));
        harness.AssertAlbumKept(rescan, baseline, files);
        Assert.Equal(0, harness.IsCompilation(baseline.AlbumId));
        Assert.Equal(harness.ArtistId(after), harness.Scalar<long>("SELECT artistId FROM Albums WHERE id = @id", new { id = baseline.AlbumId }));
        Assert.Equal(1, rescan.ArtistsDeleted);
    }

    private sealed record AlbumBaseline(long AlbumId, int TrackCount, int EventCount, int TagCount, int ListenCount);

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("CompilationSwitchRescanTests_");
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

        public void Execute(string sql, object? args = null)
        {
            _fixture.Connection.Execute(sql, args);
        }

        public void AddFile(string folderName, string name, string artist, string album)
        {
            string path = FlacTagFixture.CreateWithArtists(Path.Combine(LibraryFolder, folderName), [], [artist], name + ".flac");

            using (TagLib.File file = TagLib.File.Create(path))
            {
                file.Tag.Title = "Title " + name;
                file.Tag.Album = album;
                file.Tag.Genres = [GenreName];

                file.Save();
            }

            _paths[name] = path;
        }

        public void Retag(string name, string artist, int days)
        {
            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                file.Tag.Performers = [artist];
                file.Save();
            }

            Touch(name, days);
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

        public void Touch(string name, int days)
        {
            File.SetLastWriteTimeUtc(_paths[name], DateTime.UtcNow.AddDays(days));
        }

        public long TrackId(string name)
        {
            return Scalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}.flac" });
        }

        public long AlbumIdOf(string name)
        {
            return Scalar<long>("SELECT albumId FROM Tracks WHERE id = @id", new { id = TrackId(name) });
        }

        public long ArtistId(string name)
        {
            return Scalar<long>("SELECT id FROM Artists WHERE name = @name", new { name });
        }

        public string ArtistNameOf(string name)
        {
            return Scalar<string>("SELECT a.name FROM Tracks t JOIN Artists a ON a.id = t.artistId WHERE t.id = @id", new { id = TrackId(name) });
        }

        public string TitleOf(string name)
        {
            return Scalar<string>("SELECT title FROM Tracks WHERE id = @id", new { id = TrackId(name) });
        }

        public int IsCompilation(long albumId)
        {
            return Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId });
        }

        public List<long> AlbumIdsNamed(string album)
        {
            return _fixture.Connection.Query<long>("SELECT id FROM Albums WHERE name = @album COLLATE NOCASE ORDER BY id", new { album }).ToList();
        }

        public AlbumBaseline SeedAlbumData(string album, string[] files)
        {
            long albumId = Assert.Single(AlbumIdsNamed(album));
            DateTime now = DateTime.UtcNow;
            long tagId = _fixture.Connection.ExecuteScalar<long>("INSERT INTO Tags(name) VALUES (@name); SELECT last_insert_rowid();", new { name = "Album tag " + albumId });

            _fixture.Connection.Execute("UPDATE Albums SET isFavorite = 1, listenCount = 7, lastListen = @now WHERE id = @albumId", new { albumId, now });
            _fixture.Connection.Execute("INSERT INTO AlbumTags(albumId, tagId) VALUES (@albumId, @tagId)", new { albumId, tagId });
            _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Album', @albumId, 6.5, 0)", new { albumId });

            foreach (string file in files)
            {
                long trackId = TrackId(file);
                long artistId = Scalar<long>("SELECT artistId FROM Tracks WHERE id = @trackId", new { trackId });

                _fixture.Connection.Execute(
                    "INSERT INTO ListeningEvents(trackId, artistId, albumId, playedAt, wasSkipped, durationPlayed, durationTotal) VALUES (@trackId, @artistId, @albumId, @playedAt, 0, 120, 180)",
                    new { trackId, artistId, albumId, playedAt = now });
            }

            return new AlbumBaseline(
                albumId,
                Scalar<int>("SELECT COUNT(*) FROM Tracks"),
                files.Length,
                1,
                7);
        }

        public void AssertAlbumKept(ImportStatisticsDto rescan, AlbumBaseline baseline, string[] files, int? expectedTrackCount = null)
        {
            Assert.Equal(0, rescan.TracksDeleted);
            Assert.Equal(expectedTrackCount ?? baseline.TrackCount, Scalar<int>("SELECT COUNT(*) FROM Tracks"));
            Assert.Equal(0, rescan.AlbumsImported);
            Assert.Equal(0, rescan.AlbumsDeleted);

            long albumId = baseline.AlbumId;

            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM Albums"));
            Assert.Equal(albumId, Scalar<long>("SELECT id FROM Albums"));
            Assert.Equal(1, Scalar<int>("SELECT isFavorite FROM Albums WHERE id = @albumId", new { albumId }));
            Assert.Equal(baseline.ListenCount, Scalar<int>("SELECT listenCount FROM Albums WHERE id = @albumId", new { albumId }));
            Assert.Equal(baseline.TagCount, Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @albumId", new { albumId }));
            Assert.Equal(baseline.EventCount, Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @albumId", new { albumId }));
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @albumId", new { albumId }));

            foreach (string file in files)
            {
                Assert.Equal(albumId, AlbumIdOf(file));
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