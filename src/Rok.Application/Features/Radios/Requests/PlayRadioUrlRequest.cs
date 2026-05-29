using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioUrlRequest : IRequest<Result<bool>>
{
    public string Url { get; set; } = string.Empty;
}

public sealed class PlayRadioUrlRequestValidator : Validator<PlayRadioUrlRequest>
{
    public PlayRadioUrlRequestValidator()
    {
        Rule(x => x.Url).Required().Must(BeAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
    }

    private static bool BeAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}