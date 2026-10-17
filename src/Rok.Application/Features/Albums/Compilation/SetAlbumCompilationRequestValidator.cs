namespace Rok.Application.Features.Albums.Compilation;

public sealed class SetAlbumCompilationRequestValidator : Validator<SetAlbumCompilationRequest>
{
    public SetAlbumCompilationRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}