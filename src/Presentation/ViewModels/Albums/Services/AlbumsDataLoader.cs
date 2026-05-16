using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Tags.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Albums.Services;

public class AlbumsDataLoader(IMediator mediator, IAlbumViewModelFactory albumViewModelFactory, ILogger<AlbumsDataLoader> logger)
{
    public List<AlbumViewModel> ViewModels { get; private set; } = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public List<string> Tags { get; set; } = [];


    public async Task LoadAlbumsAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Albums loaded"))
        {
            IEnumerable<AlbumDto> albums = await mediator.Send(new GetAllAlbumsRequest());
            ViewModels = CreateAlbumsViewModels(albums);
        }
    }

    public async Task LoadGenresAsync()
    {
        IEnumerable<GenreDto> genres = await mediator.Send(new GetAllGenresRequest());
        Genres = genres.OrderBy(c => c.Name).ToList();
    }

    public async Task LoadTagsAsync()
    {
        IEnumerable<TagDto> tags = await mediator.Send(new GetAllTagsRequest());
        Tags = tags.Select(v => v.Name)
                   .Distinct()
                   .OrderBy(t => t)
                   .ToList();
    }

    public void SetAlbums(List<AlbumDto> albums)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Albums loaded"))
        {
            ViewModels = CreateAlbumsViewModels(albums);
        }
    }

    public async Task<AlbumDto?> GetAlbumByIdAsync(long id)
    {
        Result<AlbumDto> result = await mediator.Send(new GetAlbumByIdRequest(id));

        if (result.IsFailure)
        {
            logger.LogError("Failed to retrieve album {Id}: {ErrorMessage}", id, result.Errors[0]);
            return null;
        }

        return result.Value;
    }

    public void AddAlbum(AlbumDto albumDto)
    {
        AlbumViewModel viewModel = albumViewModelFactory.Create();
        viewModel.SetData(albumDto);
        ViewModels.Add(viewModel);
    }

    public void UpdateAlbum(long id, AlbumDto albumDto)
    {
        AlbumViewModel? albumToUpdate = ViewModels.FirstOrDefault(c => c.Album.Id == id);

        if (albumToUpdate != null)
        {
            albumToUpdate.SetData(albumDto);
        }
        else
        {
            logger.LogWarning("Album {Id} not found for update.", id);
        }
    }

    public void RefreshAlbumPicture(long id)
    {
        AlbumViewModel? albumToUpdate = ViewModels.FirstOrDefault(c => c.Album.Id == id);

        if (albumToUpdate != null)
        {
            albumToUpdate.LoadPicture();
        }
        else
        {
            logger.LogWarning("Album {Id} not found for picture refresh.", id);
        }
    }

    public void RemoveAlbum(long id)
    {
        ViewModels.RemoveAll(c => c.Album.Id == id);
    }

    public void Clear()
    {
        ViewModels.Clear();
        Genres.Clear();
    }

    private List<AlbumViewModel> CreateAlbumsViewModels(IEnumerable<AlbumDto> albums)
    {
        int capacity = albums.Count();
        List<AlbumViewModel> albumViewModels = new(capacity);

        foreach (AlbumDto album in albums)
        {
            AlbumViewModel albumViewModel = albumViewModelFactory.Create();
            albumViewModel.SetData(album);
            albumViewModels.Add(albumViewModel);
        }

        return albumViewModels;
    }
}