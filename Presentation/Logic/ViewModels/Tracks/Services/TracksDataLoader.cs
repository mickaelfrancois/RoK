using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;

namespace Rok.Logic.ViewModels.Tracks.Services;

public class TracksDataLoader(IMediator mediator, ILogger<TracksDataLoader> logger)
{
    public List<TrackViewModel> ViewModels { get; private set; } = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public async Task LoadTracksAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Tracks loaded"))
        {
            IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetAllTracksQuery());
            ViewModels = TrackViewModelMap.CreateViewModels(tracks);
        }
    }

    public async Task LoadGenresAsync()
    {
        IEnumerable<GenreDto> genres = await mediator.SendMessageAsync(new GetAllGenresQuery());
        Genres = genres.OrderBy(c => c.Name).ToList();
    }

    public void SetTracks(List<TrackDto> tracks)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Tracks loaded"))
        {
            ViewModels = TrackViewModelMap.CreateViewModels(tracks);
        }
    }

    public void Clear()
    {
        ViewModels.Clear();
        Genres.Clear();
    }
}