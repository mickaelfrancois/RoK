using Microsoft.Data.Sqlite;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Radios.Requests;

public class AddRadioStationRequestHandler(IRadioStationRepository repository, TimeProvider timeProvider)
    : IRequestHandler<AddRadioStationRequest, Result<long>>
{
    public async Task<Result<long>> Handle(AddRadioStationRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity entity = new()
        {
            Name = message.Name,
            StreamUrl = message.StreamUrl,
            HomepageUrl = message.HomepageUrl,
            StationUuid = message.StationUuid,
            FaviconUrl = message.FaviconUrl,
            CountryCode = message.CountryCode,
            Codec = message.Codec,
            Bitrate = message.Bitrate,
            AddedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        try
        {
            long id = await repository.AddAsync(entity, cancellationToken);
            return Result<long>.Ok(id);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Result<long>.Fail(new ConflictError("radio.duplicate", "A station with this URL already exists."));
        }
    }
}
