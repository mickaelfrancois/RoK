using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.Mapping;

public static class TrackViewModelMap
{
    public static List<TrackViewModel> CreateViewModels(IEnumerable<TrackDto> tracks)
    {
        int capacity = tracks.Count();
        List<TrackViewModel> trackViewModels = new(capacity);

        foreach (TrackDto track in tracks)
        {
            TrackViewModel trackViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
            trackViewModel.SetData(track);
            trackViewModels.Add(trackViewModel);
        }

        return trackViewModels;
    }
}
