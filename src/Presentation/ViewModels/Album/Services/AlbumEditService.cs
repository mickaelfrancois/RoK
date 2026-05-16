using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Albums.Requests;
using Rok.Dialogs;

namespace Rok.ViewModels.Album.Services;

public class AlbumEditService(IMediator mediator)
{
    public async Task<bool> EditAlbumAsync(AlbumDto album)
    {
        EditAlbumDialog dialog = new()
        {
            IsBestOf = album.IsBestOf,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            MusicBrainzID = album.MusicBrainzID,
            ReleaseGroupMusicBrainzId = album.ReleaseGroupMusicBrainzID,
            IsLock = album.IsLock,
            Biography = album.Biography,
            LastFmUrl = album.LastFmUrl,

            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return false;

        UpdateAlbumRequest command = new()
        {
            Id = album.Id,
        };

        command.IsBestOf.Set(dialog.IsBestOf);
        command.IsLive.Set(dialog.IsLive);
        command.IsCompilation.Set(dialog.IsCompilation);
        command.MusicBrainzID.Set(dialog.MusicBrainzID);
        command.ReleaseGroupMusicBrainzID.Set(dialog.ReleaseGroupMusicBrainzId);
        command.Biography.Set(dialog.Biography);
        command.IsLock.Set(dialog.IsLock);
        command.LastFmUrl.Set(dialog.LastFmUrl);

        await mediator.Send(command);

        album.IsBestOf = dialog.IsBestOf;
        album.IsLive = dialog.IsLive;
        album.IsCompilation = dialog.IsCompilation;
        album.MusicBrainzID = dialog.MusicBrainzID;
        album.ReleaseGroupMusicBrainzID = dialog.ReleaseGroupMusicBrainzId;
        album.Biography = dialog.Biography;
        album.IsLock = dialog.IsLock;
        album.LastFmUrl = dialog.LastFmUrl;

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