using Rok.Application.Features.Tracks.Services;

namespace Rok.Application.Features.Tracks.Requests;

/// <summary>
/// Changes the title, track number, disc, composers or BPM of a track in its file tag and in the database. A null field is left untouched.
/// </summary>
/// <param name="trackId">Id of the track.</param>
/// <param name="title">New title, or null to keep the current one.</param>
/// <param name="trackNumber">New track number, or null to keep the current one.</param>
/// <param name="disc">New disc number, or null to keep the current one.</param>
/// <param name="composers">New composers, an empty string to remove them, or null to keep the current ones.</param>
/// <param name="bpm">New beats per minute, 0 to remove it, or null to keep the current one.</param>
public class UpdateTrackTagsRequest(long trackId, string? title = null, int? trackNumber = null, int? disc = null, string? composers = null, int? bpm = null) : IRequest<Result<TrackEditSaved>>
{
    public long TrackId { get; init; } = trackId;

    public string? Title { get; init; } = title;

    public int? TrackNumber { get; init; } = trackNumber;

    public int? Disc { get; init; } = disc;

    public string? Composers { get; init; } = composers;

    public int? Bpm { get; init; } = bpm;

    public bool HasChanges => Title is not null || TrackNumber is not null || Disc is not null || Composers is not null || Bpm is not null;
}