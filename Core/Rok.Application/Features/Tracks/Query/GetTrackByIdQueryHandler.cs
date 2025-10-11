using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTrackByIdQuery(long id) : IQuery<Result<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}


public class GetTrackByIdQueryHandler(ITrackRepository trackRepository) : IQueryHandler<GetTrackByIdQuery, Result<TrackDto>>
{
    public async Task<Result<TrackDto>> HandleAsync(GetTrackByIdQuery query, CancellationToken cancellationToken)
    {
        TrackEntity? track = await trackRepository.GetByIdAsync(query.Id);
        if (track == null)
            return Result<TrackDto>.Fail("NotFound", "Track not found");
        else
            return Result<TrackDto>.Success(TrackDtoMapping.Map(track));
    }
}