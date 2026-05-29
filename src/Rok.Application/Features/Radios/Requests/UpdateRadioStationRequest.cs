namespace Rok.Application.Features.Radios.Requests;

public class UpdateRadioStationRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }
}

public sealed class UpdateRadioStationRequestValidator : Validator<UpdateRadioStationRequest>
{
    public UpdateRadioStationRequestValidator()
    {
        Rule(x => x.Id).Must(id => id > 0).Message("Id must be positive.");
        Rule(x => x.Name).Required().MaxLength(200);
        Rule(x => x.StreamUrl).Required().Must(BeAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
    }

    private static bool BeAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
