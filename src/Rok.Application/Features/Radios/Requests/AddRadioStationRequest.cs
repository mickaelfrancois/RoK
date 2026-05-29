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
        Rule(x => x.StreamUrl).Required().Must(BeAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
        Rule(x => x.FaviconUrl).Must(BeAbsoluteHttpUriOrNull).Message("Must be an absolute http(s) URL or empty.");
        Rule(x => x.StationUuid).MaxLength(64);
        Rule(x => x.CountryCode).MaxLength(2);
        Rule(x => x.Codec).MaxLength(20);
        Rule(x => x.Bitrate).Must(b => b is null or >= 0).Message("Bitrate must be positive.");
    }

    private static bool BeAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool BeAbsoluteHttpUriOrNull(string? value) =>
        string.IsNullOrEmpty(value) || BeAbsoluteHttpUri(value);
}
