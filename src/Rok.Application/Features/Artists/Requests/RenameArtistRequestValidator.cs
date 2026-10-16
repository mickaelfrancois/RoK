using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

public sealed class RenameArtistRequestValidator : Validator<RenameArtistRequest>
{
    public RenameArtistRequestValidator()
    {
        Rule(x => x.ArtistId).GreaterThan(0L);
        Rule(x => x.NewName).Must(name => !string.IsNullOrWhiteSpace(name)).Message("The artist name must not be blank.");
        Rule(x => x.NewName).Must(name => name is null || name.Length <= ArtistRenameLimits.MaxNameLength).Message($"The artist name must not exceed {ArtistRenameLimits.MaxNameLength} characters.");
    }
}