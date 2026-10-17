using Microsoft.Extensions.Logging;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Tags;

/// <summary>
/// Checks the files of some tracks, then writes tag changes in them one by one and verifies each requested field.
/// Shared by the artist rename and merge and by the album edit and merge.
/// </summary>
public class TrackFileTagWriter
{
    private readonly ITagWriter _tagWriter;
    private readonly IPlayerService _playerService;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="tagWriter">Writes and checks the tags of the files.</param>
    /// <param name="playerService">Tells which track is playing.</param>
    /// <param name="logger">Receives the write incidents.</param>
    public TrackFileTagWriter(ITagWriter tagWriter, IPlayerService playerService, ILogger<TrackFileTagWriter> logger)
        : this(tagWriter, playerService, (ILogger)logger)
    {
    }

    internal TrackFileTagWriter(ITagWriter tagWriter, IPlayerService playerService, ILogger logger)
    {
        _tagWriter = tagWriter;
        _playerService = playerService;
        _logger = logger;
    }

    /// <summary>
    /// Checks every file and lists what forbids a write: the playing track and the reasons of the check.
    /// </summary>
    /// <param name="tracks">Tracks whose files are checked and may be written.</param>
    /// <param name="playbackOnlyTracks">Tracks only tested for playback, their files are never checked nor written.</param>
    /// <param name="cancellationToken">Token observed while checking.</param>
    /// <param name="classify">Optional extra rule that may turn a readable file into a blocker.</param>
    /// <returns>The files that can be written and the blockers.</returns>
    public async Task<TrackFileAnalysis> AnalyzeAsync(
        IReadOnlyList<TrackEntity> tracks,
        IReadOnlyList<TrackEntity> playbackOnlyTracks,
        CancellationToken cancellationToken,
        Func<TrackEntity, TagWriteCheck, ArtistRenameBlocker?>? classify = null)
    {
        List<ArtistRenameBlocker> blockers = [];
        IReadOnlyList<TagWriteCheck> checks = await _tagWriter.CheckAsync(tracks.Select(t => t.MusicFile).ToList(), cancellationToken);
        TrackDto? playing = _playerService.CurrentTrack;
        List<CheckedTrackFile> files = [];

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

            ArtistRenameBlocker? extra = classify?.Invoke(track, check);

            if (extra is not null)
            {
                blockers.Add(extra);
                continue;
            }

            files.Add(new CheckedTrackFile(track, check));
        }

        foreach (TrackEntity track in playbackOnlyTracks)
        {
            if (IsPlaying(playing, track))
                blockers.Add(new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.Playing, null));
        }

        return new TrackFileAnalysis(files, blockers);
    }

    /// <summary>
    /// Writes the planned changes one file at a time. A failing file is listed and the loop goes on.
    /// </summary>
    /// <param name="writes">Changes to write, one per file.</param>
    /// <param name="onWritten">Called with the track id, file date and size of every verified file.</param>
    /// <param name="progress">Receives the progress of the writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The files that could not be written.</returns>
    public async Task<List<ArtistRenameFailure>> WriteAsync(
        IReadOnlyList<PlannedFileWrite> writes,
        Func<long, DateTimeOffset?, long?, Task> onWritten,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<ArtistRenameFailure> failures = [];
        int done = 0;

        foreach (PlannedFileWrite write in writes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failures.Add(new ArtistRenameFailure(write.Track.MusicFile, ETagWriteReason.Cancelled));
                continue;
            }

            ETagWriteReason failure = await WriteFileAsync(write, onWritten);

            if (failure != ETagWriteReason.None)
                failures.Add(new ArtistRenameFailure(write.Track.MusicFile, failure));

            done++;
            progress?.Report(new ArtistRenameProgress(done, writes.Count));
        }

        return failures;
    }

    private async Task<ETagWriteReason> WriteFileAsync(PlannedFileWrite write, Func<long, DateTimeOffset?, long?, Task> onWritten)
    {
        TagWriteRequest request = new(write.Track.MusicFile, write.Changes);
        TagWriteResult result;

        try
        {
            IReadOnlyList<TagWriteResult> results = await _tagWriter.WriteAsync([request], CancellationToken.None);
            result = results[0];

            if (result.Outcome != ETagWriteOutcome.Succeeded)
                return result.Reason == ETagWriteReason.None ? ETagWriteReason.IoError : result.Reason;

            if (!IsVerified(write.Changes, result.Values))
                return ETagWriteReason.VerificationFailed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write the tags in {File}.", write.Track.MusicFile);

            return ETagWriteReason.IoError;
        }

        try
        {
            await onWritten(write.Track.Id, result.FileDateModified, result.Size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tags written in the file of track id {Id} but the follow-up could not be stored. The next rescan will realign it.", write.Track.Id);
        }

        return ETagWriteReason.None;
    }

    private static bool IsVerified(TagChanges changes, TagFieldValues? values)
    {
        bool anyVerifiable = changes.Artist is not null || changes.Album is not null || changes.Genre is not null || changes.Year is not null;

        if (!anyVerifiable)
            return true;

        if (values is null)
            return false;

        return (changes.Artist is null || string.Equals(values.Artist, changes.Artist, StringComparison.Ordinal))
            && (changes.Album is null || string.Equals(values.Album, changes.Album, StringComparison.Ordinal))
            && (changes.Genre is null || string.Equals(values.Genre, changes.Genre, StringComparison.Ordinal))
            && (changes.Year is null || values.Year == changes.Year);
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
/// Changes to write in the file of one track.
/// </summary>
/// <param name="Track">Track owning the file.</param>
/// <param name="Changes">Tag changes to write.</param>
public sealed record PlannedFileWrite(TrackEntity Track, TagChanges Changes);

/// <summary>
/// A file that passed the check.
/// </summary>
/// <param name="Track">Track owning the file.</param>
/// <param name="Check">Result of the check.</param>
public sealed record CheckedTrackFile(TrackEntity Track, TagWriteCheck Check);

/// <summary>
/// Result of the check of some files.
/// </summary>
/// <param name="Files">Files that passed the check, in the order of the tracks.</param>
/// <param name="Blockers">Reasons that forbid the change.</param>
public sealed record TrackFileAnalysis(IReadOnlyList<CheckedTrackFile> Files, IReadOnlyList<ArtistRenameBlocker> Blockers);