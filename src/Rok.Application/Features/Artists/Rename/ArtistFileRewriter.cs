using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Checks the files of an artist, then writes the artist name in their tags.
/// Shared by the rename and the merge: both turn the files that carry the old name into files that carry the new one.
/// </summary>
public class ArtistFileRewriter(
    ITagWriter tagWriter,
    ITrackRepository trackRepository,
    IPlayerService playerService,
    ILogger<ArtistFileRewriter> logger)
{
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
        List<ArtistRenameBlocker> blockers = [];
        IReadOnlyList<TagWriteCheck> checks = await tagWriter.CheckAsync(tracks.Select(t => t.MusicFile).ToList(), cancellationToken);
        TrackDto? playing = playerService.CurrentTrack;
        List<ArtistFileState> files = [];

        for (int i = 0; i < tracks.Count; i++)
        {
            TrackEntity track = tracks[i];
            TagWriteCheck check = checks[i];

            if (IsPlaying(playing, track))
            {
                blockers.Add(new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.Playing, null));
                continue;
            }

            if (check.Reason != ETagWriteReason.None)
            {
                blockers.Add(new ArtistRenameBlocker(track.MusicFile, ToBlock(check.Reason), check.Reason.ToString()));
                continue;
            }

            bool? needsWrite = NeedsWrite(check.CurrentArtist, oldName, newName);

            if (needsWrite is null)
            {
                blockers.Add(new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, check.CurrentArtist));
                continue;
            }

            files.Add(new ArtistFileState(track, check, needsWrite.Value));
        }

        foreach (TrackEntity track in playbackOnlyTracks)
        {
            if (IsPlaying(playing, track))
                blockers.Add(new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.Playing, null));
        }

        return new ArtistFileAnalysis(tracks, files, blockers);
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
        List<ArtistRenameFailure> failures = [];
        List<ArtistFileState> toWrite = analysis.Files.Where(f => f.NeedsWrite).ToList();
        int done = 0;

        if (storeInfoOfDoneFiles)
        {
            foreach (ArtistFileState file in analysis.Files.Where(f => !f.NeedsWrite))
                await StoreFileInfoAsync(file.Track.Id, file.Check.FileDateModified, file.Check.Size);
        }

        foreach (ArtistFileState file in toWrite)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failures.Add(new ArtistRenameFailure(file.Track.MusicFile, ETagWriteReason.Cancelled));
                continue;
            }

            ETagWriteReason failure = await WriteFileAsync(newName, file);

            if (failure != ETagWriteReason.None)
                failures.Add(new ArtistRenameFailure(file.Track.MusicFile, failure));

            done++;
            progress?.Report(new ArtistRenameProgress(done, toWrite.Count));
        }

        return failures;
    }

    private async Task<ETagWriteReason> WriteFileAsync(string newName, ArtistFileState file)
    {
        TagWriteRequest request = new(file.Track.MusicFile, new TagChanges { Artist = newName });

        try
        {
            IReadOnlyList<TagWriteResult> results = await tagWriter.WriteAsync([request], CancellationToken.None);
            TagWriteResult result = results[0];

            if (result.Outcome != ETagWriteOutcome.Succeeded)
                return result.Reason == ETagWriteReason.None ? ETagWriteReason.IoError : result.Reason;

            if (result.Values is null || !string.Equals(result.Values.Artist, newName, StringComparison.Ordinal))
                return ETagWriteReason.VerificationFailed;

            await StoreFileInfoAsync(file.Track.Id, result.FileDateModified, result.Size);

            return ETagWriteReason.None;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write the artist name in {File}.", file.Track.MusicFile);

            return ETagWriteReason.IoError;
        }
    }

    private async Task StoreFileInfoAsync(long trackId, DateTimeOffset? fileDate, long? size)
    {
        if (fileDate is null || size is null)
            return;

        try
        {
            await trackRepository.UpdateFileInfoAsync(trackId, fileDate.Value.DateTime, size.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Artist written in the file of track id {Id} but its file info could not be stored. The next rescan will realign it.", trackId);
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

    private static bool IsPlaying(TrackDto? current, TrackEntity track)
    {
        if (current is null)
            return false;

        return current.Id == track.Id || string.Equals(current.MusicFile, track.MusicFile, StringComparison.OrdinalIgnoreCase);
    }

    private static EArtistRenameBlock ToBlock(ETagWriteReason reason)
    {
        return reason switch
        {
            ETagWriteReason.NotFound => EArtistRenameBlock.NotFound,
            ETagWriteReason.ReadOnly => EArtistRenameBlock.ReadOnly,
            ETagWriteReason.CloudPlaceholder => EArtistRenameBlock.CloudPlaceholder,
            ETagWriteReason.UnsupportedFormat => EArtistRenameBlock.UnsupportedFormat,
            ETagWriteReason.InsufficientDiskSpace => EArtistRenameBlock.InsufficientDiskSpace,
            ETagWriteReason.UnsupportedTagVersion => EArtistRenameBlock.UnsupportedTagVersion,
            ETagWriteReason.Playing => EArtistRenameBlock.Playing,
            ETagWriteReason.ImportInProgress => EArtistRenameBlock.ImportInProgress,
            _ => EArtistRenameBlock.Locked
        };
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