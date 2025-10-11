using Rok.Logic.ViewModels.Albums;

namespace Rok.Logic.Mapping;

public static class AlbumViewModelMap
{
    public static List<AlbumViewModel> CreateViewModels(IEnumerable<AlbumDto> albums)
    {
        int capacity = albums.Count();
        List<AlbumViewModel> albumViewModels = new(capacity);

        foreach (AlbumDto album in albums)
        {
            AlbumViewModel albumViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
            albumViewModel.SetData(album);
            albumViewModels.Add(albumViewModel);
        }

        return albumViewModels;
    }
}
