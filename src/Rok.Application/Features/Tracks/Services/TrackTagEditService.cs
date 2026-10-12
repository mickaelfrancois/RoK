using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Tracks.Services;

/// <summary>
/// Edits track titles: checks the rules, writes the audio files, then updates the database.
/// </summary>
public class TrackTagEditService(
    ITrackRepository trackRepository,
    ITagWriter tagWriter,
    IPlayerService playerService,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<TrackTagEditService> logger)
{
    /// <summary>
    /// Writes new titles in the tags of the track files and in the database.
    /// </summary>
    /// <param name="changes">Track ids with their new title.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>One result per change, in the same order.</returns>
    public async Task<IReadOnlyList<TrackTagEditResult>> UpdateTitlesAsync(IReadOnlyList<(long TrackId, string Title)> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (!appOptions.WriteTagsEnabled)
            return changes.Select(c => TrackTagEditResult.Disabled(c.TrackId, c.Title)).ToList();

        TrackTagEditResult?[] results = new TrackTagEditResult?[changes.Count];
        List<(int Index, TrackEntity Track)> candidates = await SelectCandidatesAsync(changes, results);

        if (candidates.Count == 0)
            return Complete(results);

        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
            {
                foreach ((int index, _) in candidates)
                    results[index] = TrackTagEditResult.Skip(changes[index].TrackId, changes[index].Title, ETagWriteReason.ImportInProgress);

                return Complete(results);
            }

            List<TagWriteRequest> requests = candidates
                .Select(c => new TagWriteRequest(c.Track.MusicFile, new TagChanges { Title = changes[c.Index].Title }))
                .ToList();

            IReadOnlyList<TagWriteResult> writeResults = await tagWriter.WriteAsync(requests, cancellationToken);

            for (int i = 0; i < candidates.Count; i++)
            {
                int index = candidates[i].Index;
                results[index] = await ApplyWriteResultAsync(changes[index], writeResults[i]);
            }
        }
        finally
        {
            lease?.Dispose();
        }

        return Complete(results);
    }

    private async Task<List<(int Index, TrackEntity Track)>> SelectCandidatesAsync(IReadOnlyList<(long TrackId, string Title)> changes, TrackTagEditResult?[] results)
    {
        List<(int Index, TrackEntity Track)> candidates = [];
        TrackDto? current = playerService.CurrentTrack;

        for (int i = 0; i < changes.Count; i++)
        {
            (long trackId, string title) = changes[i];
            TrackEntity? track = await trackRepository.GetByIdAsync(trackId);

            if (track is null)
            {
                results[i] = TrackTagEditResult.Skip(trackId, title, ETagWriteReason.NotFound);
                continue;
            }

            if (IsPlaying(current, track))
            {
                results[i] = TrackTagEditResult.Skip(trackId, title, ETagWriteReason.Playing);
                continue;
            }

            candidates.Add((i, track));
        }

        return candidates;
    }

    private static bool IsPlaying(TrackDto? current, TrackEntity track)
    {
        if (current is null)
            return false;

        return current.Id == track.Id || string.Equals(current.MusicFile, track.MusicFile, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<TrackTagEditResult> ApplyWriteResultAsync((long TrackId, string Title) change, TagWriteResult writeResult)
    {
        if (writeResult.Outcome == ETagWriteOutcome.Skipped)
            return TrackTagEditResult.Skip(change.TrackId, change.Title, writeResult.Reason);

        if (writeResult.Outcome == ETagWriteOutcome.Failed)
            return TrackTagEditResult.Fail(change.TrackId, change.Title, writeResult.Reason);

        if (writeResult.FileDateModified is null || writeResult.Size is null)
        {
            logger.LogError("Tag written for track id {Id} without file information.", change.TrackId);

            return TrackTagEditResult.Fail(change.TrackId, change.Title, ETagWriteReason.IoError);
        }

        try
        {
            bool updated = await trackRepository.UpdateTitleAndFileInfoAsync(change.TrackId, change.Title, writeResult.FileDateModified.Value.DateTime, writeResult.Size.Value);

            if (updated)
                return TrackTagEditResult.Succeed(change.TrackId, change.Title);

            logger.LogError("Tag written but database not updated for track id {Id}.", change.TrackId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tag written but database update failed for track id {Id}.", change.TrackId);
        }

        return TrackTagEditResult.Fail(change.TrackId, change.Title, ETagWriteReason.IoError);
    }

    private static List<TrackTagEditResult> Complete(TrackTagEditResult?[] results)
    {
        return results.Select(r => r!).ToList();
    }
}