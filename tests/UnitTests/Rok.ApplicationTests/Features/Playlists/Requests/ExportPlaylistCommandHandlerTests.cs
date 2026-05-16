using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;

namespace Rok.ApplicationTests.Features.Playlists.Requests;

public class ExportPlaylistRequestHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistFormatResolver> _resolver = new();
    private readonly Mock<IPlaylistFormatWriter> _writer = new();

    private (string TmpDir, string FinalPath) PrepareTempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return (dir, Path.Combine(dir, "out.m3u8"));
    }

    private ExportPlaylistRequestHandler BuildHandler()
    {
        IPlaylistFormatWriter? writer = _writer.Object;
        _resolver.Setup(r => r.TryGetWriter(It.IsAny<string>(), out writer)).Returns(true);
        return new ExportPlaylistRequestHandler(_mediator.Object, _resolver.Object, NullLogger<ExportPlaylistRequestHandler>.Instance);
    }

    [Fact(DisplayName = "writes_classic_playlist_with_all_tracks_in_order")]
    public async Task Writes_classic_playlist_with_all_tracks_in_order()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Mix", Type = 1 }));
            _mediator.Setup(m => m.Send(It.IsAny<GetTracksByPlaylistIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new[]
                     {
                         new TrackDto { Title = "T1", ArtistName = "A1", MusicFile = "D:\\1.mp3", Duration = 100 },
                         new TrackDto { Title = "T2", ArtistName = "A2", MusicFile = "D:\\2.mp3", Duration = 200 }
                     });

            PlaylistFileModel? captured = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((_, m, _) => captured = m)
                   .Returns(Task.CompletedTask);

            ExportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result result = await sut.Handle(new ExportPlaylistRequest(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal("Mix", captured!.Name);
            Assert.Equal(2, captured.Entries.Count);
            Assert.Equal("D:\\1.mp3", captured.Entries[0].FilePath);
            Assert.Equal(TimeSpan.FromSeconds(100), captured.Entries[0].Duration);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "writes_smart_playlist_using_persisted_tracks_only")]
    public async Task Writes_smart_playlist_using_persisted_tracks_only()
    {
        // Arrange — Smart (Type=0). Handler should NOT regenerate; same query path.
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Smart", Type = 0 }));
            _mediator.Setup(m => m.Send(It.IsAny<GetTracksByPlaylistIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new[] { new TrackDto { Title = "T", ArtistName = "A", MusicFile = "D:\\s.mp3", Duration = 10 } });
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            ExportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result result = await sut.Handle(new ExportPlaylistRequest(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _mediator.Verify(m => m.Send(It.IsAny<GeneratePlaylistTracksRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "empty_playlist_writes_only_extm3u_header")]
    public async Task Empty_playlist_writes_only_extm3u_header()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Empty", Type = 1 }));
            _mediator.Setup(m => m.Send(It.IsAny<GetTracksByPlaylistIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());

            PlaylistFileModel? captured = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((_, m, _) => captured = m)
                   .Returns(Task.CompletedTask);

            ExportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result result = await sut.Handle(new ExportPlaylistRequest(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Empty(captured!.Entries);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "playlist_not_found_returns_fail")]
    public async Task Playlist_not_found_returns_fail()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Fail("NotFound"));

            ExportPlaylistRequestHandler sut = BuildHandler();

            // Act
            Result result = await sut.Handle(new ExportPlaylistRequest(1, final), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            _writer.Verify(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "unsupported_extension_returns_fail")]
    public async Task Unsupported_extension_returns_fail()
    {
        // Arrange
        (string dir, string _) = PrepareTempFile();
        string weirdPath = Path.Combine(dir, "out.foo");
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "X", Type = 1 }));
            _mediator.Setup(m => m.Send(It.IsAny<GetTracksByPlaylistIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());
            IPlaylistFormatWriter? noWriter = null;
            _resolver.Setup(r => r.TryGetWriter(It.IsAny<string>(), out noWriter)).Returns(false);

            ExportPlaylistRequestHandler sut = new(_mediator.Object, _resolver.Object, NullLogger<ExportPlaylistRequestHandler>.Instance);

            // Act
            Result result = await sut.Handle(new ExportPlaylistRequest(1, weirdPath), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "atomic_write_uses_tmp_then_move")]
    public async Task Atomic_write_uses_tmp_then_move()
    {
        // Arrange — verify that WriteAsync receives a stream pointing at a .tmp file path adjacent to the final path
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "X", Type = 1 }));
            _mediator.Setup(m => m.Send(It.IsAny<GetTracksByPlaylistIdRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());

            string? tmpSeen = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((s, _, _) =>
                   {
                       if (s is FileStream fs)
                           tmpSeen = fs.Name;
                   })
                   .Returns(Task.CompletedTask);

            ExportPlaylistRequestHandler sut = BuildHandler();

            // Act
            await sut.Handle(new ExportPlaylistRequest(1, final), CancellationToken.None);

            // Assert
            Assert.NotNull(tmpSeen);
            Assert.EndsWith(".tmp", tmpSeen);
            Assert.True(File.Exists(final));
            Assert.False(File.Exists(tmpSeen!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
