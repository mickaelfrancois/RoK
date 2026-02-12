using Rok.Logic.ViewModels.Artists.Interfaces;

namespace Rok.Logic.ViewModels.Artists.Services;

public class ArtistViewModelFactory(IServiceProvider serviceProvider) : IArtistViewModelFactory
{
    public ArtistViewModel Create()
    {
        return serviceProvider.GetRequiredService<ArtistViewModel>();
    }
}