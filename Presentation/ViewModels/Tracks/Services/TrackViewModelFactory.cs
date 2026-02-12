using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Tracks.Services;

public class TrackViewModelFactory(IServiceProvider serviceProvider) : ITrackViewModelFactory
{
    public TrackViewModel Create()
    {
        return serviceProvider.GetRequiredService<TrackViewModel>();
    }
}