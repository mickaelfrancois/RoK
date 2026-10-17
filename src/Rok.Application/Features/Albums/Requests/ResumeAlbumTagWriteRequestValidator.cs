namespace Rok.Application.Features.Albums.Requests;

public sealed class ResumeAlbumTagWriteRequestValidator : Validator<ResumeAlbumTagWriteRequest>
{
    public ResumeAlbumTagWriteRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}