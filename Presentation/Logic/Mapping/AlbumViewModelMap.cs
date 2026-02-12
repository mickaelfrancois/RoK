using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Albums.Interfaces;

namespace Rok.Logic.Mapping;

public static class AlbumViewModelMap
{
    public static List<AlbumViewModel> CreateViewModels(IEnumerable<AlbumDto> albums, IAlbumViewModelFactory albumViewModelFactory)
    {
        int capacity = albums.Count();
        List<AlbumViewModel> albumViewModels = new(capacity);

        foreach (AlbumDto album in albums)
        {
            AlbumViewModel albumViewModel = albumViewModelFactory.Create();
            albumViewModel.SetData(album);
            albumViewModels.Add(albumViewModel);
        }

        return albumViewModels;
    }
}
