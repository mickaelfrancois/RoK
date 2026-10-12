namespace Rok.Application.Features.Tracks.Requests;

/// <summary>
/// Changes the title of a track in its file tag and in the database.
/// </summary>
/// <param name="trackId">Id of the track.</param>
/// <param name="title">New title.</param>
public class UpdateTrackTitleRequest(long trackId, string title) : IRequest<Result<string>>
{
    public long TrackId { get; init; } = trackId;

    public string Title { get; init; } = title;
}