using Rok.Application.Features.Albums.Requests;

namespace Rok.ViewModels.Album.Services;

public class AlbumEditService(IMediator mediator, IDialogService dialogService)
{
    public async Task<bool> EditAlbumAsync(AlbumDto album)
    {
        AlbumEditValues current = new()
        {
            IsBestOf = album.IsBestOf,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            MusicBrainzID = album.MusicBrainzID,
            ReleaseGroupMusicBrainzID = album.ReleaseGroupMusicBrainzID,
            IsLock = album.IsLock,
            Biography = album.Biography,
            LastFmUrl = album.LastFmUrl
        };

        AlbumEditValues? edited = await dialogService.ShowEditAlbumAsync(current);

        if (edited is null)
            return false;

        UpdateAlbumRequest command = new()
        {
            Id = album.Id,
        };

        command.IsBestOf.Set(edited.IsBestOf);
        command.IsLive.Set(edited.IsLive);
        command.IsCompilation.Set(edited.IsCompilation);
        command.MusicBrainzID.Set(edited.MusicBrainzID);
        command.ReleaseGroupMusicBrainzID.Set(edited.ReleaseGroupMusicBrainzID);
        command.Biography.Set(edited.Biography);
        command.IsLock.Set(edited.IsLock);
        command.LastFmUrl.Set(edited.LastFmUrl);

        await mediator.Send(command);

        album.IsBestOf = edited.IsBestOf;
        album.IsLive = edited.IsLive;
        album.IsCompilation = edited.IsCompilation;
        album.MusicBrainzID = edited.MusicBrainzID;
        album.ReleaseGroupMusicBrainzID = edited.ReleaseGroupMusicBrainzID;
        album.Biography = edited.Biography;
        album.IsLock = edited.IsLock;
        album.LastFmUrl = edited.LastFmUrl;

        return true;
    }

    public async Task UpdateFavoriteAsync(AlbumDto album, bool isFavorite)
    {
        await mediator.Send(new UpdateAlbumFavoriteRequest(album.Id, isFavorite));
        album.IsFavorite = isFavorite;
    }

    public Task UpdateTagsAsync(long id, IEnumerable<string> tags)
    {
        return mediator.Send(new UpdateAlbumTagsRequest(id, tags));
    }

    public Task UpdatePictureDominantColorAsync(long id, long? colorValue)
    {
        return mediator.Send(new UpdateAlbumPictureDominantColorRequest(id, colorValue));
    }
}