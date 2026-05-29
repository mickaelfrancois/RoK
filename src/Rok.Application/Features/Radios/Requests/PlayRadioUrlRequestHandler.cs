using Rok.Application.Features.Radios.Services;
using Rok.Application.Player;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioUrlRequestHandler(
    IRadioStreamUrlResolver resolver,
    IPlayerService playerService,
    TimeProvider timeProvider)
    : IRequestHandler<PlayRadioUrlRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(PlayRadioUrlRequest message, CancellationToken cancellationToken)
    {
        Result<string> resolved = await resolver.ResolveAsync(message.Url, cancellationToken);

        if (resolved.IsFailure)
            return Result<bool>.Fail(resolved.Errors);

        RadioStationDto adHoc = new(
            Id: 0,
            Name: "Ad-hoc stream",
            StreamUrl: resolved.Value,
            HomepageUrl: null,
            StationUuid: null,
            FaviconUrl: null,
            CountryCode: null,
            Codec: null,
            Bitrate: null,
            AddedAt: timeProvider.GetUtcNow().UtcDateTime,
            LastListen: null);

        playerService.PlayRadioStation(adHoc);
        return Result<bool>.Ok(true);
    }
}