using Moq;
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists;

namespace Rok.Infrastructure.UnitTests.Playlists;

public class PlaylistFormatResolverTests
{
    private static IPlaylistFormatReader BuildReader()
    {
        Mock<IPlaylistFormatReader> reader = new();
        reader.SetupGet(r => r.Format).Returns(ExportPlaylistFormat.M3u8);
        return reader.Object;
    }

    private static IPlaylistFormatWriter BuildWriter()
    {
        Mock<IPlaylistFormatWriter> writer = new();
        writer.SetupGet(w => w.Format).Returns(ExportPlaylistFormat.M3u8);
        return writer.Object;
    }

    [Fact(DisplayName = "resolves_reader_for_m3u8_extension")]
    public void Resolves_reader_for_m3u8_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool found = sut.TryGetReader(".m3u8", out IPlaylistFormatReader? reader);

        // Assert
        Assert.True(found);
        Assert.NotNull(reader);
    }

    [Fact(DisplayName = "resolves_reader_for_m3u_extension")]
    public void Resolves_reader_for_m3u_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool found = sut.TryGetReader(".m3u", out IPlaylistFormatReader? reader);

        // Assert
        Assert.True(found);
        Assert.NotNull(reader);
    }

    [Fact(DisplayName = "extension_match_is_case_insensitive")]
    public void Extension_match_is_case_insensitive()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool readerFound = sut.TryGetReader(".M3U8", out _);
        bool writerFound = sut.TryGetWriter(".M3U8", out _);

        // Assert
        Assert.True(readerFound);
        Assert.True(writerFound);
    }

    [Fact(DisplayName = "returns_false_for_unknown_extension")]
    public void Returns_false_for_unknown_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool readerFound = sut.TryGetReader(".pls", out IPlaylistFormatReader? reader);
        bool writerFound = sut.TryGetWriter(".m3u", out IPlaylistFormatWriter? writer);

        // Assert
        Assert.False(readerFound);
        Assert.Null(reader);
        Assert.False(writerFound);
        Assert.Null(writer);
    }
}
