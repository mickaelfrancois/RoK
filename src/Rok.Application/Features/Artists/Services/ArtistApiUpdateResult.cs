namespace Rok.Application.Features.Artists.Services;

public readonly record struct ArtistApiUpdateResult(bool DataUpdated, bool PictureDownloaded, bool BackdropsDownloaded)
{
    public static ArtistApiUpdateResult None => new(false, false, false);
}