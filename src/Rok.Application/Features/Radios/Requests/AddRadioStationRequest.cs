namespace Rok.Application.Features.Radios.Requests;

public class AddRadioStationRequest : IRequest<Result<long>>
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }

    public string? StationUuid { get; set; }

    public string? FaviconUrl { get; set; }

    public string? CountryCode { get; set; }

    public string? Codec { get; set; }

    public int? Bitrate { get; set; }
}

public sealed class AddRadioStationRequestValidator : Validator<AddRadioStationRequest>
{
    public AddRadioStationRequestValidator()
    {
        Rule(x => x.Name).Required().MaxLength(200);
        Rule(x => x.StreamUrl).Required().Must(HttpUriValidation.IsAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
        Rule(x => x.FaviconUrl).Must(HttpUriValidation.IsAbsoluteHttpUriOrNull).Message("Must be an absolute http(s) URL or empty.");
        Rule(x => x.StationUuid).MaxLength(64);
        Rule(x => x.CountryCode).MaxLength(2);
        Rule(x => x.Codec).MaxLength(20);
        Rule(x => x.Bitrate).Must(b => b is null or >= 0).Message("Bitrate must be positive.");
    }
}