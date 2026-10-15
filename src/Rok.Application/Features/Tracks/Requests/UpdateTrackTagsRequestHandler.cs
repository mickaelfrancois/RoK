using Rok.Application.Features.Tracks.Services;

namespace Rok.Application.Features.Tracks.Requests;

internal class UpdateTrackTagsRequestHandler(TrackTagEditService trackTagEditService) : IRequestHandler<UpdateTrackTagsRequest, Result<TrackEditSaved>>
{
    public async Task<Result<TrackEditSaved>> Handle(UpdateTrackTagsRequest request, CancellationToken cancellationToken)
    {
        TrackTagsEdit edit = new(request.TrackId, request.Title?.Trim(), request.TrackNumber, request.Disc, request.Composers?.Trim(), request.Bpm);

        IReadOnlyList<TrackTagEditResult> results = await trackTagEditService.UpdateAsync([edit], cancellationToken);
        TrackTagEditResult result = results[0];

        if (result.Succeeded && result.Saved is not null)
            return Result<TrackEditSaved>.Ok(result.Saved);

        string code = result.ErrorCode ?? TrackTagEditResult.CodeFor(result.Reason);

        return Result<TrackEditSaved>.Fail(new OperationError(code, $"Failed to write the tags of track {request.TrackId}: {result.Reason}."));
    }
}