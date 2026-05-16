using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Messages;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Playlists.Requests;

public class ImportPlaylistRequestHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IPlaylistFormatResolver> _resolver = new();
    private readonly Mock<IPlaylistFormatReader> _reader = new();
    private readonly Mock<ITrackRepository> _trackRepository = new();
    private readonly IMessenger _messenger = new Messenger();

    public ImportPlaylistRequestHandlerTests()
    {
        _connection = new SqliteConnection($"Data Source=ImportHandler_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _connection.Open();
        _connection.Execute("CREATE TABLE playlists (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, picture TEXT, duration INTEGER, trackCount INTEGER, trackMaximum INTEGER, durationMaximum INTEGER, groupsJson TEXT, type INTEGER, creatDate TEXT, editDate TEXT)");
        _connection.Execute("CREATE TABLE playlisttracks (id INTEGER PRIMARY KEY AUTOINCREMENT, playlistId INTEGER, trackId INTEGER, position INTEGER, listened INTEGER, creatDate TEXT)");
    }

    public void Dispose() => _connection.Dispose();

    private ImportPlaylistRequestHandler BuildHandler()
    {
        IPlaylistFormatReader? reader = _reader.Object;
        _resolver.Setup(r => r.TryGetReader(It.IsAny<string>(), out reader)).Returns(true);
        return new ImportPlaylistRequestHandler(_resolver.Object, _trackRepository.Object, _connection, _messenger, NullLogger<ImportPlaylistRequestHandler>.Instance);
    }

    private static PlaylistFileModel Model(string name, params (string Path, string? Title, string? Artist, TimeSpan? Duration)[] entries)
        => new(name, entries.Select(e => new PlaylistFileEntry(e.Path, e.Title, e.Artist, e.Duration)).ToList());

    private string WritePlaylistFile(string content, string fileName = "test.m3u8")
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{fileName}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact(DisplayName = "creates_playlist_with_only_matched_tracks")]
    public async Task Creates_playlist_with_only_matched_tracks()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix",
                        ("D:\\a.mp3", "A", "Aa", TimeSpan.FromSeconds(100)),
                        ("D:\\miss.mp3", null, null, null),
                        ("D:\\b.mp3", "B", "Bb", TimeSpan.FromSeconds(50))));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 11, Duration = 100 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\miss.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\b.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 12, Duration = 50 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(PlaylistImportStatus.Imported, result.Value.Status);
            Assert.Equal(2, result.Value.MatchedCount);
            Assert.Equal(1, result.Value.IgnoredCount);

            int trackRows = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlisttracks WHERE playlistId = @id", new { id = result.Value.PlaylistId });
            Assert.Equal(2, trackRows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "skipped_when_zero_tracks_match")]
    public async Task Skipped_when_zero_tracks_match()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Empty", ("D:\\miss.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(PlaylistImportStatus.Skipped, result.Value.Status);
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "name_collision_is_suffixed_with_paren_two")]
    public async Task Name_collision_is_suffixed_with_paren_two()
    {
        // Arrange
        _connection.Execute("INSERT INTO playlists(name, type, creatDate) VALUES (@name, 1, @now)", new { name = "Mix", now = DateTime.UtcNow });
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1, Duration = 10 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Mix (2)", result.Value.FinalName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "track_positions_are_zero_based_and_sequential")]
    public async Task Track_positions_are_zero_based_and_sequential()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Order",
                        ("D:\\1.mp3", null, null, null),
                        ("D:\\2.mp3", null, null, null),
                        ("D:\\3.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\1.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 100 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\2.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 101 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\3.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 102 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            List<int> positions = _connection.Query<int>("SELECT position FROM playlisttracks WHERE playlistId = @id ORDER BY position", new { id = result.Value.PlaylistId }).ToList();
            Assert.Equal(new[] { 0, 1, 2 }, positions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "unsupported_extension_returns_fail")]
    public async Task Unsupported_extension_returns_fail()
    {
        // Arrange
        IPlaylistFormatReader? nullReader = null;
        _resolver.Setup(r => r.TryGetReader(It.IsAny<string>(), out nullReader)).Returns(false);
        string path = WritePlaylistFile("ignored", "weird.foo");
        try
        {
            ImportPlaylistRequestHandler sut = new(_resolver.Object, _trackRepository.Object, _connection, _messenger, NullLogger<ImportPlaylistRequestHandler>.Instance);

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "parse_error_returns_failed_without_inserting")]
    public async Task Parse_error_returns_failed_without_inserting()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidDataException("garbage"));

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "cancellation_propagates_and_rolls_back")]
    public async Task Cancellation_propagates_and_rolls_back()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new OperationCanceledException());

            ImportPlaylistRequestHandler sut = BuildHandler();

            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            // Act + Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => sut.Handle(new ImportPlaylistRequest(path), cts.Token));
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "name_collision_walks_until_free_slot")]
    public async Task Name_collision_walks_until_free_slot()
    {
        // Arrange — pre-fill "Mix", "Mix (2)", "Mix (3)" so import lands on "Mix (4)"
        DateTime now = DateTime.UtcNow;
        foreach (string n in new[] { "Mix", "Mix (2)", "Mix (3)" })
            _connection.Execute("INSERT INTO playlists(name, type, creatDate) VALUES (@name, 1, @now)", new { name = n, now });

        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Mix (4)", result.Value.FinalName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "ignored_count_reflects_unmatched_paths")]
    public async Task Ignored_count_reflects_unmatched_paths()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix",
                        ("D:\\a.mp3", null, null, null),
                        ("D:\\b.mp3", null, null, null),
                        ("D:\\c.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\b.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\c.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.MatchedCount);
            Assert.Equal(2, result.Value.IgnoredCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "skipped_does_not_create_header")]
    public async Task Skipped_does_not_create_header()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Empty"));

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "db_error_during_track_insert_rolls_back_header")]
    public async Task Db_error_during_track_insert_rolls_back_header()
    {
        // Arrange — drop playlisttracks so the inserts fail mid-transaction
        _connection.Execute("DROP TABLE playlisttracks");
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Boom", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "publishes_playlist_imported_message_on_success")]
    public async Task Publishes_playlist_imported_message_on_success()
    {
        // Arrange
        long? receivedId = null;
        void Listener(PlaylistImportedMessage m) => receivedId = m.PlaylistId;
        using IDisposable subscription = _messenger.Subscribe<PlaylistImportedMessage>(Listener);

        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

            ImportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.Handle(new ImportPlaylistRequest(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(result.Value.PlaylistId, receivedId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
