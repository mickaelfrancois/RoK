namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioUrlRequest : IRequest<Result<bool>>
{
    public string Url { get; set; } = string.Empty;
}

public sealed class PlayRadioUrlRequestValidator : Validator<PlayRadioUrlRequest>
{
    public PlayRadioUrlRequestValidator()
    {
        Rule(x => x.Url).Required().Must(HttpUriValidation.IsAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
    }
}