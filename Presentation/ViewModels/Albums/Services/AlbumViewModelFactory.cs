using Rok.Logic.ViewModels.Albums;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Albums.Services;

public class AlbumViewModelFactory(IServiceProvider serviceProvider) : IAlbumViewModelFactory
{
    public AlbumViewModel Create()
    {
        return serviceProvider.GetRequiredService<AlbumViewModel>();
    }
}