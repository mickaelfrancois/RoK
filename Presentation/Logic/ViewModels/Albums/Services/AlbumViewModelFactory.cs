using Rok.Logic.ViewModels.Albums.Interfaces;

namespace Rok.Logic.ViewModels.Albums.Services;

public class AlbumViewModelFactory(IServiceProvider serviceProvider) : IAlbumViewModelFactory
{
    public AlbumViewModel Create()
    {
        return serviceProvider.GetRequiredService<AlbumViewModel>();
    }
}