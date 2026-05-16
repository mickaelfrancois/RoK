using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTrackByIdRequest(long id) : IRequest<Result<TrackDto>>
{
    public long Id { get; } = id;
}


public sealed class GetTrackByIdRequestValidator : Validator<GetTrackByIdRequest>
{
    public GetTrackByIdRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}


public class GetTrackByIdRequestHandler(ITrackRepository trackRepository) : IRequestHandler<GetTrackByIdRequest, Result<TrackDto>>
{
    public async Task<Result<TrackDto>> Handle(GetTrackByIdRequest query, CancellationToken cancellationToken)
    {
        TrackEntity? track = await trackRepository.GetByIdAsync(query.Id);
        if (track == null)
            return Result<TrackDto>.Fail(NotFoundError.ForEntity("Track", query.Id));
        else
            return Result<TrackDto>.Ok(TrackDtoMapping.Map(track));
    }
}
