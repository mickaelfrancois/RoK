using Rok.Application.Dto;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Application.Features.Artists.Services;

public interface IArtistApiService
{
    Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as the overload without a mode, with an explicit merge mode.
    /// </summary>
    Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture, ArtistApiMergeMode mode, CancellationToken cancellationToken = default);
}