using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;

namespace Rok.ViewModels.Artists.Services;

public class ArtistViewModelFactory(IServiceProvider serviceProvider) : IArtistViewModelFactory
{
    public ArtistViewModel Create()
    {
        return serviceProvider.GetRequiredService<ArtistViewModel>();
    }
}