using Rok.Application.Features.Tracks.Services;

namespace Rok.Application.Features.Tracks.Requests;

internal class UpdateTrackTitleRequestHandler(TrackTagEditService trackTagEditService) : IRequestHandler<UpdateTrackTitleRequest, Result<string>>
{
    public async Task<Result<string>> Handle(UpdateTrackTitleRequest request, CancellationToken cancellationToken)
    {
        string title = request.Title.Trim();

        IReadOnlyList<TrackTagEditResult> results = await trackTagEditService.UpdateTitlesAsync([(request.TrackId, title)], cancellationToken);
        TrackTagEditResult result = results[0];

        if (result.Succeeded)
            return Result<string>.Ok(title);

        return Result<string>.Fail(new OperationError(result.ErrorCode ?? TrackTagEditResult.CodeFor(result.Reason), $"Failed to write the title of track {request.TrackId}: {result.Reason}."));
    }
}