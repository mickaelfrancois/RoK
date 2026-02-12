using Rok.Logic.ViewModels.Tracks.Interfaces;

namespace Rok.Logic.ViewModels.Tracks.Services;

public class TrackViewModelFactory(IServiceProvider serviceProvider) : ITrackViewModelFactory
{
    public TrackViewModel Create()
    {
        return serviceProvider.GetRequiredService<TrackViewModel>();
    }
}