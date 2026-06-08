using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using Rok.Application.Tag;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class EmbeddedLyricsImporterTests
{
    private readonly Mock<ILyricsService> _lyricsService = new();

    private EmbeddedLyricsImporter CreateSut() => new(_lyricsService.Object, NullLogger<EmbeddedLyricsImporter>.Instance);

    [Fact(DisplayName = "when_the_tag_has_no_embedded_lyrics_no_sidecar_is_written")]
    public async Task NoEmbeddedLyrics_WritesNothing()
    {
        // Arrange
        EmbeddedLyricsImporter sut = CreateSut();
        TrackFile file = new() { FullPath = @"C:\music\song.mp3", Lyrics = "   " };

        // Act
        await sut.ExtractAsync(file);

        // Assert
        _lyricsService.Verify(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>()), Times.Never);
    }

    [Fact(DisplayName = "when_a_sidecar_already_exists_no_sidecar_is_written")]
    public async Task ExistingSidecar_WritesNothing()
    {
        // Arrange
        _lyricsService.Setup(s => s.CheckLyricsFileExists(It.IsAny<string>())).Returns(ELyricsType.Plain);

        EmbeddedLyricsImporter sut = CreateSut();
        TrackFile file = new() { FullPath = @"C:\music\song.mp3", Lyrics = "Some embedded lyrics" };

        // Act
        await sut.ExtractAsync(file);

        // Assert
        _lyricsService.Verify(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>()), Times.Never);
    }

    [Fact(DisplayName = "when_embedded_lyrics_are_plain_text_a_txt_sidecar_is_written")]
    public async Task PlainLyrics_WritesTxtSidecar()
    {
        // Arrange
        const string content = "Hello world\nSecond line";

        _lyricsService.Setup(s => s.CheckLyricsFileExists(It.IsAny<string>())).Returns(ELyricsType.None);
        _lyricsService.Setup(s => s.GetPlainLyricsFileName(It.IsAny<string>())).Returns(@"C:\music\song.txt");

        LyricsModel? saved = null;
        _lyricsService.Setup(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>()))
                      .Callback<LyricsModel>(m => saved = m)
                      .Returns(Task.CompletedTask);

        EmbeddedLyricsImporter sut = CreateSut();
        TrackFile file = new() { FullPath = @"C:\music\song.mp3", Lyrics = content };

        // Act
        await sut.ExtractAsync(file);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(@"C:\music\song.txt", saved!.File);
        Assert.Equal(content, saved.PlainLyrics);
        Assert.Equal(ELyricsType.Plain, saved.LyricsType);
        _lyricsService.Verify(s => s.GetSynchronizedLyricsFileName(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "when_embedded_lyrics_carry_timestamps_an_lrc_sidecar_is_written")]
    public async Task SynchronizedLyrics_WritesLrcSidecar()
    {
        // Arrange
        const string content = "[00:12.34]Hello\n[00:15.00]World";

        _lyricsService.Setup(s => s.CheckLyricsFileExists(It.IsAny<string>())).Returns(ELyricsType.None);
        _lyricsService.Setup(s => s.GetSynchronizedLyricsFileName(It.IsAny<string>())).Returns(@"C:\music\song.lrc");

        LyricsModel? saved = null;
        _lyricsService.Setup(s => s.SaveLyricsAsync(It.IsAny<LyricsModel>()))
                      .Callback<LyricsModel>(m => saved = m)
                      .Returns(Task.CompletedTask);

        EmbeddedLyricsImporter sut = CreateSut();
        TrackFile file = new() { FullPath = @"C:\music\song.mp3", Lyrics = content };

        // Act
        await sut.ExtractAsync(file);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(@"C:\music\song.lrc", saved!.File);
        Assert.Equal(content, saved.PlainLyrics);
        Assert.Equal(ELyricsType.Synchronized, saved.LyricsType);
        _lyricsService.Verify(s => s.GetPlainLyricsFileName(It.IsAny<string>()), Times.Never);
    }
}
