namespace Rok.Application.Features.Albums.Services;

public readonly record struct AlbumApiUpdateResult(bool DataUpdated, bool PictureDownloaded)
{
    public static AlbumApiUpdateResult None => new(false, false);
}
