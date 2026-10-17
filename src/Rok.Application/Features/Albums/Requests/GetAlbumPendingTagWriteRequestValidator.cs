namespace Rok.Application.Features.Albums.Requests;

public sealed class GetAlbumPendingTagWriteRequestValidator : Validator<GetAlbumPendingTagWriteRequest>
{
    public GetAlbumPendingTagWriteRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}