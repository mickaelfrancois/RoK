using Microsoft.Extensions.Logging;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Checks the files of an artist, then writes the artist name in their tags.
/// Shared by the rename and the merge: both turn the files that carry the old name into files that carry the new one.
/// The checking and the writing are delegated to <see cref="TrackFileTagWriter"/>.
/// </summary>
public class ArtistFileRewriter
{
    private readonly TrackFileTagWriter _writer;
    private readonly ITrackRepository _trackRepository;
    private readonly ILogger<ArtistFileRewriter> _logger;

    /// <summary>
    /// Creates the rewriter.
    /// </summary>
    /// <param name="tagWriter">Writes and checks the tags of the files.</param>
    /// <param name="trackRepository">Stores the file date and size of the written files.</param>
    /// <param name="playerService">Tells which track is playing.</param>
    /// <param name="logger">Receives the write incidents.</param>
    public ArtistFileRewriter(
        ITagWriter tagWriter,
        ITrackRepository trackRepository,
        IPlayerService playerService,
        ILogger<ArtistFileRewriter> logger)
    {
        _writer = new TrackFileTagWriter(tagWriter, playerService, logger);
        _trackRepository = trackRepository;
        _logger = logger;
    }

    /// <summary>
    /// Checks every file and tells which ones still carry the old name.
    /// </summary>
    /// <param name="tracks">Tracks whose files are checked and may be written.</param>
    /// <param name="oldName">Name the files carry before the change.</param>
    /// <param name="newName">Name the files carry after the change.</param>
    /// <param name="playbackOnlyTracks">Tracks only tested for playback, their files are never written.</param>
    /// <param name="cancellationToken">Token observed while checking.</param>
    /// <returns>The files to write or already done, and the blockers.</returns>
    public async Task<ArtistFileAnalysis> AnalyzeAsync(
        IReadOnlyList<TrackEntity> tracks,
        string oldName,
        string newName,
        IReadOnlyList<TrackEntity> playbackOnlyTracks,
        CancellationToken cancellationToken)
    {
        TrackFileAnalysis analysis = await _writer.AnalyzeAsync(
            tracks,
            playbackOnlyTracks,
            cancellationToken,
            (track, check) => NeedsWrite(check.CurrentArtist, oldName, newName) is null
                ? new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, check.CurrentArtist)
                : null);

        List<ArtistFileState> files = analysis.Files
            .Select(f => new ArtistFileState(f.Track, f.Check, NeedsWrite(f.Check.CurrentArtist, oldName, newName)!.Value))
            .ToList();

        return new ArtistFileAnalysis(tracks, files, analysis.Blockers);
    }

    /// <summary>
    /// Writes the new name in the files that still carry the old one. A failing file is listed and the loop goes on.
    /// </summary>
    /// <param name="analysis">Result of <see cref="AnalyzeAsync"/>.</param>
    /// <param name="newName">Name to write.</param>
    /// <param name="storeInfoOfDoneFiles">Stores the file date and size of the files that already carry the new name.</param>
    /// <param name="progress">Receives the progress of the writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The files that could not be written.</returns>
    public async Task<List<ArtistRenameFailure>> WriteAsync(ArtistFileAnalysis analysis, string newName, bool storeInfoOfDoneFiles, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        List<PlannedFileWrite> toWrite = analysis.Files
            .Where(f => f.NeedsWrite)
            .Select(f => new PlannedFileWrite(f.Track, new TagChanges { Artist = newName }))
            .ToList();

        if (storeInfoOfDoneFiles)
        {
            foreach (ArtistFileState file in analysis.Files.Where(f => !f.NeedsWrite))
                await StoreFileInfoAsync(file.Track.Id, file.Check.FileDateModified, file.Check.Size);
        }

        return await _writer.WriteAsync(toWrite, StoreFileInfoAsync, progress, cancellationToken);
    }

    private async Task StoreFileInfoAsync(long trackId, DateTimeOffset? fileDate, long? size)
    {
        if (fileDate is null || size is null)
            return;

        try
        {
            await _trackRepository.UpdateFileInfoAsync(trackId, fileDate.Value.DateTime, size.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Artist written in the file of track id {Id} but its file info could not be stored. The next rescan will realign it.", trackId);
        }
    }

    private static bool? NeedsWrite(string? currentArtist, string oldName, string newName)
    {
        string current = currentArtist ?? string.Empty;

        if (string.Equals(current, newName, StringComparison.Ordinal))
            return false;

        if (ArtistNameRules.HasSameKey(current, oldName))
            return true;

        if (ArtistNameRules.HasSameKey(current, newName))
            return false;

        return null;
    }
}

/// <summary>
/// A checked file of an artist.
/// </summary>
/// <param name="Track">Track owning the file.</param>
/// <param name="Check">Result of the check.</param>
/// <param name="NeedsWrite">True when the file still carries the old name.</param>
public sealed record ArtistFileState(TrackEntity Track, TagWriteCheck Check, bool NeedsWrite);

/// <summary>
/// Result of the check of the files of an artist.
/// </summary>
/// <param name="Tracks">Tracks that were checked.</param>
/// <param name="Files">Files that can be written or are already done.</param>
/// <param name="Blockers">Reasons that forbid the change.</param>
public sealed record ArtistFileAnalysis(IReadOnlyList<TrackEntity> Tracks, IReadOnlyList<ArtistFileState> Files, IReadOnlyList<ArtistRenameBlocker> Blockers);