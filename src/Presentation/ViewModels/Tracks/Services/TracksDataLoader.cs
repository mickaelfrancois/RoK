using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Tracks.Services;

public class TracksDataLoader(IMediator mediator, ITrackViewModelFactory trackViewModelFactory, ILogger<TracksDataLoader> logger)
{
    public List<TrackViewModel> ViewModels { get; private set; } = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public async Task LoadTracksAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Tracks loaded"))
        {
            IEnumerable<TrackDto> tracks = await mediator.Send(new GetAllTracksRequest());

            List<TrackDto> trackList = tracks as List<TrackDto> ?? tracks.ToList();

            using (new PerfLogger(logger).Parameters($"Tracks: VM create ({trackList.Count})"))
            {
                ViewModels = TrackViewModelMap.CreateViewModels(trackList, trackViewModelFactory);
            }
        }
    }

    public async Task LoadGenresAsync()
    {
        IEnumerable<GenreDto> genres = await mediator.Send(new GetAllGenresRequest());
        Genres = genres.OrderBy(c => c.Name).ToList();
    }

    public void SetTracks(List<TrackDto> tracks)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Tracks loaded"))
        {
            ViewModels = TrackViewModelMap.CreateViewModels(tracks, trackViewModelFactory);
        }
    }

    public void Clear()
    {
        ViewModels.Clear();
        Genres.Clear();
    }
}