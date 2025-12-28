using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Albums.Command;
using Rok.Dialogs;

namespace Rok.Logic.ViewModels.Album.Services;

public class AlbumEditService(IMediator mediator)
{
    public async Task<bool> EditAlbumAsync(AlbumDto album)
    {
        EditAlbumDialog dialog = new()
        {
            IsBestOf = album.IsBestOf,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return false;

        UpdateAlbumCommand command = new()
        {
            Id = album.Id,
            IsBestOf = dialog.IsBestOf,
            IsLive = dialog.IsLive,
            IsCompilation = dialog.IsCompilation,

            AllMusicID = album.AllMusicID,
            AmazonID = album.AmazonID,
            AudioDbArtistID = album.AudioDbArtistID,
            AudioDbID = album.AudioDbID,
            Biography = album.Biography,
            DiscogsID = album.DiscogsID,
            GeniusID = album.GeniusID,
            Label = album.Label,
            LyricWikiID = album.LyricWikiID,
            MusicBrainzID = album.MusicBrainzID,
            MusicMozID = album.MusicMozID,
            ReleaseDate = album.ReleaseDate,
            ReleaseGroupMusicBrainzID = album.ReleaseGroupMusicBrainzID,
            Sales = album.Sales,
            Wikipedia = album.Wikipedia,
            WikipediaID = album.WikipediaID,
            WikidataID = album.WikidataID
        };

        await mediator.SendMessageAsync(command);

        album.IsBestOf = dialog.IsBestOf;
        album.IsLive = dialog.IsLive;
        album.IsCompilation = dialog.IsCompilation;

        return true;
    }

    public async Task UpdateFavoriteAsync(AlbumDto album, bool isFavorite)
    {
        await mediator.SendMessageAsync(new UpdateAlbumFavoriteCommand(album.Id, isFavorite));
        album.IsFavorite = isFavorite;
    }
}