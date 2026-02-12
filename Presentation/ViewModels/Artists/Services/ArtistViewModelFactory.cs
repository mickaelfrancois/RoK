using Rok.Logic.ViewModels.Artists;
using Rok.ViewModels.Artists.Interfaces;

namespace Rok.ViewModels.Artists.Services;

public class ArtistViewModelFactory(IServiceProvider serviceProvider) : IArtistViewModelFactory
{
    public ArtistViewModel Create()
    {
        return serviceProvider.GetRequiredService<ArtistViewModel>();
    }
}