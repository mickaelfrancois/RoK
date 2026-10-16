using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Tracks.Services;

/// <summary>
/// Edits track tags: checks the rules, writes the audio files, then updates the database.
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
    /// Writes the edited fields in the tags of the track files, then stores in the database the values read back from the files.
    /// </summary>
    /// <param name="edits">Track ids with the fields to change.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>One result per edit, in the same order.</returns>
    public async Task<IReadOnlyList<TrackTagEditResult>> UpdateAsync(IReadOnlyList<TrackTagsEdit> edits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (!appOptions.WriteTagsEnabled)
            return edits.Select(e => TrackTagEditResult.Disabled(e.TrackId, e.Title)).ToList();

        TrackTagEditResult?[] results = new TrackTagEditResult?[edits.Count];
        List<(int Index, TrackEntity Track)> candidates = await SelectCandidatesAsync(edits, results);

        if (candidates.Count == 0)
            return Complete(results);

        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
            {
                foreach ((int index, _) in candidates)
                    results[index] = TrackTagEditResult.Skip(edits[index].TrackId, edits[index].Title, ETagWriteReason.ImportInProgress);

                return Complete(results);
            }

            List<TagWriteRequest> requests = candidates
                .Select(c => new TagWriteRequest(c.Track.MusicFile, ToChanges(edits[c.Index])))
                .ToList();

            IReadOnlyList<TagWriteResult> writeResults = await tagWriter.WriteAsync(requests, cancellationToken);

            for (int i = 0; i < candidates.Count; i++)
            {
                int index = candidates[i].Index;
                results[index] = await ApplyWriteResultAsync(edits[index], writeResults[i]);
            }
        }
        finally
        {
            lease?.Dispose();
        }

        return Complete(results);
    }

    private static TagChanges ToChanges(TrackTagsEdit edit)
    {
        return new TagChanges
        {
            Title = edit.Title,
            TrackNumber = edit.TrackNumber,
            Disc = edit.Disc,
            Composers = edit.Composers,
            Bpm = edit.Bpm
        };
    }

    private async Task<List<(int Index, TrackEntity Track)>> SelectCandidatesAsync(IReadOnlyList<TrackTagsEdit> edits, TrackTagEditResult?[] results)
    {
        List<(int Index, TrackEntity Track)> candidates = [];
        TrackDto? current = playerService.CurrentTrack;

        for (int i = 0; i < edits.Count; i++)
        {
            TrackTagsEdit edit = edits[i];

            if (!ToChanges(edit).HasChanges)
            {
                results[i] = TrackTagEditResult.Skip(edit.TrackId, edit.Title, ETagWriteReason.Cancelled);
                continue;
            }

            TrackEntity? track = await trackRepository.GetByIdAsync(edit.TrackId);

            if (track is null)
            {
                results[i] = TrackTagEditResult.Skip(edit.TrackId, edit.Title, ETagWriteReason.NotFound);
                continue;
            }

            if (IsPlaying(current, track))
            {
                results[i] = TrackTagEditResult.Skip(edit.TrackId, edit.Title, ETagWriteReason.Playing);
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

    private async Task<TrackTagEditResult> ApplyWriteResultAsync(TrackTagsEdit edit, TagWriteResult writeResult)
    {
        if (writeResult.Outcome == ETagWriteOutcome.Skipped)
            return TrackTagEditResult.Skip(edit.TrackId, edit.Title, writeResult.Reason);

        if (writeResult.Outcome == ETagWriteOutcome.Failed)
            return TrackTagEditResult.Fail(edit.TrackId, edit.Title, writeResult.Reason);

        if (writeResult.FileDateModified is null || writeResult.Size is null || writeResult.Values is null)
        {
            logger.LogError("Tag already written in the file of track id {Id} but the values could not be read back, so the database is not updated. The next rescan will realign it.", edit.TrackId);

            return TrackTagEditResult.Fail(edit.TrackId, edit.Title, ETagWriteReason.IoError);
        }

        TagFieldValues values = writeResult.Values;
        DateTime fileDate = writeResult.FileDateModified.Value.DateTime;
        long size = writeResult.Size.Value;

        try
        {
            bool updated = await trackRepository.UpdateTagFieldsAndFileInfoAsync(edit.TrackId, values, fileDate, size);

            if (updated)
            {
                string? composers = string.IsNullOrEmpty(values.Composers) ? null : values.Composers;
                TrackEditSaved saved = new(values.Title, values.TrackNumber, values.Disc, composers, values.Bpm, fileDate, size);

                return TrackTagEditResult.Succeed(edit.TrackId, edit.Title, saved);
            }

            logger.LogError("Tag written but database not updated for track id {Id}.", edit.TrackId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tag written but database update failed for track id {Id}.", edit.TrackId);
        }

        return TrackTagEditResult.Fail(edit.TrackId, edit.Title, ETagWriteReason.IoError);
    }

    private static List<TrackTagEditResult> Complete(TrackTagEditResult?[] results)
    {
        return results.Select(r => r!).ToList();
    }
}