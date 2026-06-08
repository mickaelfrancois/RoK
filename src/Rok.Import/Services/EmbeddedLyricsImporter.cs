using System.Text.RegularExpressions;
using CleanArch.DevKit.Guards;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using Rok.Application.Tag;

namespace Rok.Import.Services;

/// <summary>
/// Writes the lyrics embedded in an audio tag to a sidecar file during import:
/// a <c>.lrc</c> file when the content carries <c>[mm:ss]</c> timestamps, a
/// <c>.txt</c> file otherwise. The sidecar is written only when neither a
/// <c>.lrc</c> nor a <c>.txt</c> file already exists for the track, so that
/// API-fetched or hand-curated lyrics are never overwritten.
/// </summary>
public partial class EmbeddedLyricsImporter(ILyricsService lyricsService, ILogger<EmbeddedLyricsImporter> logger)
{
    [GeneratedRegex(@"^\s*\[\d{1,2}:\d{2}", RegexOptions.Compiled | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SynchronizedTimestampRegex();

    /// <summary>
    /// Indicates whether <see cref="ExtractAsync"/> would write a sidecar for this file
    /// (embedded lyrics present and no <c>.lrc</c>/<c>.txt</c> sidecar yet). Lets callers
    /// report a planned write without performing it.
    /// </summary>
    public bool WouldWriteSidecar(TrackFile file)
    {
        Guard.NotNull(file);

        if (string.IsNullOrWhiteSpace(file.Lyrics))
            return false;

        if (string.IsNullOrEmpty(file.FullPath))
            return false;

        return lyricsService.CheckLyricsFileExists(file.FullPath) == ELyricsType.None;
    }

    /// <summary>
    /// Writes a lyrics sidecar from the embedded tag when appropriate (see <see cref="WouldWriteSidecar"/>):
    /// a <c>.lrc</c> when the content carries <c>[mm:ss]</c> timestamps, a <c>.txt</c> otherwise.
    /// </summary>
    /// <returns><see langword="true"/> when a sidecar was written, <see langword="false"/> otherwise.</returns>
    public async Task<bool> ExtractAsync(TrackFile file, CancellationToken cancellationToken = default)
    {
        if (!WouldWriteSidecar(file))
            return false;

        bool isSynchronized = SynchronizedTimestampRegex().IsMatch(file.Lyrics);

        string fileName = isSynchronized
            ? lyricsService.GetSynchronizedLyricsFileName(file.FullPath)
            : lyricsService.GetPlainLyricsFileName(file.FullPath);

        try
        {
            await lyricsService.SaveLyricsAsync(new LyricsModel
            {
                File = fileName,
                PlainLyrics = file.Lyrics,
                LyricsType = isSynchronized ? ELyricsType.Synchronized : ELyricsType.Plain
            });

            logger.LogTrace("Embedded lyrics extracted to {File}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write embedded lyrics sidecar for '{File}'", file.FullPath);
            return false;
        }
    }
}
