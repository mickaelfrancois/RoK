using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Requests;

public sealed class GetAlbumEditPreviewRequestValidator : Validator<GetAlbumEditPreviewRequest>
{
    public GetAlbumEditPreviewRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
        Rule(x => x.Name).Must(name => !string.IsNullOrWhiteSpace(name)).Message("The album name must not be blank.");
        Rule(x => x.Name).Must(name => name is null || name.Length <= AlbumEditLimits.MaxNameLength).Message($"The album name must not exceed {AlbumEditLimits.MaxNameLength} characters.");
        Rule(x => x.ArtistName).Must(name => !string.IsNullOrWhiteSpace(name)).Message("The artist name must not be blank.");
        Rule(x => x.ArtistName).Must(name => name is null || name.Length <= AlbumEditLimits.MaxNameLength).Message($"The artist name must not exceed {AlbumEditLimits.MaxNameLength} characters.");
        Rule(x => x.Year).Must(year => year is null || (year >= AlbumEditLimits.MinYear && year <= AlbumEditLimits.MaxYear)).Message($"The year must be between {AlbumEditLimits.MinYear} and {AlbumEditLimits.MaxYear}.");
        Rule(x => x.GenreName).Must(genre => genre is null || (!string.IsNullOrWhiteSpace(genre) && genre.Length <= AlbumEditLimits.MaxNameLength)).Message($"The genre name must not be blank nor exceed {AlbumEditLimits.MaxNameLength} characters.");
    }
}