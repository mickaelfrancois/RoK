using Rok.Application.Features.Genres.Query;
using Rok.Logic.ViewModels.Artists;
using Rok.ViewModels.Artists.Interfaces;

namespace Rok.ViewModels.Artists.Services;

public class ArtistsDataLoader(IMediator mediator, IArtistViewModelFactory artistViewModelFactory, ILogger<ArtistsDataLoader> logger)
{
    public List<ArtistViewModel> ViewModels { get; private set; } = [];

    public List<GenreDto> Genres { get; private set; } = [];


    public async Task LoadArtistsAsync(bool excludeArtistsWithoutAlbum)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Artists loaded"))
        {
            IEnumerable<ArtistDto> artists = await mediator.SendMessageAsync(new GetAllArtistsQuery { ExcludeArtistsWithoutAlbum = excludeArtistsWithoutAlbum });
            ViewModels = CreateArtistsViewModels(artists);
        }
    }

    public async Task LoadGenresAsync()
    {
        IEnumerable<GenreDto> genres = await mediator.SendMessageAsync(new GetAllGenresQuery());
        Genres = genres.OrderBy(c => c.Name).ToList();
    }

    public void SetArtists(List<ArtistDto> artists)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Artists loaded"))
        {
            ViewModels = CreateArtistsViewModels(artists);
        }
    }

    public async Task<ArtistDto?> GetArtistByIdAsync(long id)
    {
        Result<ArtistDto> result = await mediator.SendMessageAsync(new GetArtistByIdQuery(id));

        if (result.IsError)
        {
            logger.LogError("Failed to retrieve artist {Id}: {ErrorMessage}", id, result.Error);
            return null;
        }

        return result.Value;
    }

    public void AddArtist(ArtistDto artistDto)
    {
        ArtistViewModel viewModel = artistViewModelFactory.Create();
        viewModel.SetData(artistDto);
        ViewModels.Add(viewModel);
    }

    public void UpdateArtist(long id, ArtistDto artistDto)
    {
        ArtistViewModel? artistToUpdate = ViewModels.FirstOrDefault(c => c.Artist.Id == id);

        if (artistToUpdate != null)
        {
            artistToUpdate.SetData(artistDto);
        }
        else
        {
            logger.LogWarning("Artist {Id} not found for update.", id);
        }
    }

    public void RefreshArtistPicture(long id)
    {
        ArtistViewModel? artistToUpdate = ViewModels.FirstOrDefault(c => c.Artist.Id == id);

        if (artistToUpdate != null)
        {
            artistToUpdate.LoadPicture();
        }
        else
        {
            logger.LogWarning("Artist {Id} not found for picture refresh.", id);
        }
    }

    public void RemoveArtist(long id)
    {
        ViewModels.RemoveAll(c => c.Artist.Id == id);
    }

    public void Clear()
    {
        ViewModels.Clear();
        Genres.Clear();
    }

    private List<ArtistViewModel> CreateArtistsViewModels(IEnumerable<ArtistDto> artists)
    {
        int capacity = artists.Count();
        List<ArtistViewModel> artistViewModels = new(capacity);

        foreach (ArtistDto artist in artists)
        {
            ArtistViewModel viewModel = artistViewModelFactory.Create();
            viewModel.SetData(artist);
            artistViewModels.Add(viewModel);
        }

        return artistViewModels;
    }
}