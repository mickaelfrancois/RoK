using Rok.Logic.ViewModels.Tracks;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.Mapping;

public static class TrackViewModelMap
{
    public static List<TrackViewModel> CreateViewModels(IEnumerable<TrackDto> tracks, ITrackViewModelFactory trackViewModelFactory)
    {
        int capacity = tracks.Count();
        List<TrackViewModel> trackViewModels = new(capacity);

        foreach (TrackDto track in tracks)
        {
            TrackViewModel trackViewModel = trackViewModelFactory.Create();
            trackViewModel.SetData(track);
            trackViewModels.Add(trackViewModel);
        }

        return trackViewModels;
    }
}
