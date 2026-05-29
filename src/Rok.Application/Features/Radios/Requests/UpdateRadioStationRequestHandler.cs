using Microsoft.Data.Sqlite;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Radios.Requests;

public class UpdateRadioStationRequestHandler(IRadioStationRepository repository)
    : IRequestHandler<UpdateRadioStationRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateRadioStationRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity? existing = await repository.GetByIdAsync(message.Id, cancellationToken);

        if (existing is null)
            return Result<bool>.Fail(new NotFoundError("radio.not_found", $"Radio station {message.Id} not found."));

        string? homepageUrl = string.IsNullOrWhiteSpace(message.HomepageUrl) ? null : message.HomepageUrl.Trim();

        try
        {
            await repository.UpdateAsync(message.Id, message.Name.Trim(), message.StreamUrl.Trim(), homepageUrl, cancellationToken);
            return Result<bool>.Ok(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Result<bool>.Fail(new ConflictError("radio.duplicate", "A station with this URL already exists."));
        }
    }
}
