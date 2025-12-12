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

        PatchAlbumCommand patchAlbumCommand = new()
        {
            Id = album.Id,
            IsBestOf = new PatchField<bool>(dialog.IsBestOf),
            IsLive = new PatchField<bool>(dialog.IsLive),
            IsCompilation = new PatchField<bool>(dialog.IsCompilation)
        };

        await mediator.SendMessageAsync(patchAlbumCommand);

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