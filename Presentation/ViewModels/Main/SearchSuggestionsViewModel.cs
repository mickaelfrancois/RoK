using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Search.Query;

namespace Rok.ViewModels.Main;

public partial class SearchSuggestionsViewModel(IMediator mediator) : ObservableObject
{
    private readonly IMediator _mediator = mediator;
    private System.Threading.CancellationTokenSource? _debounceCts;

    public ObservableCollection<AlbumDto> AlbumSuggestions { get; } = new ObservableCollection<AlbumDto>();
    public ObservableCollection<ArtistDto> ArtistSuggestions { get; } = new ObservableCollection<ArtistDto>();
    public ObservableCollection<TrackDto> TrackSuggestions { get; } = new ObservableCollection<TrackDto>();

    private bool _hasResults;
    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public async Task UpdateSuggestionsAsync(string keyword)
    {
        if (_debounceCts != null)
        {
            await _debounceCts.CancelAsync();
            _debounceCts.Dispose();
        }
        _debounceCts = new System.Threading.CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
        {
            ClearSuggestions();
            return;
        }

        try
        {
            await Task.Delay(300, _debounceCts.Token);

            SearchDto result = await _mediator.SendMessageAsync(new SearchQuery { Name = keyword });

            AlbumSuggestions.Clear();
            ArtistSuggestions.Clear();
            TrackSuggestions.Clear();

            foreach (AlbumDto album in result.Albums.Take(3))
                AlbumSuggestions.Add(album);

            foreach (ArtistDto artist in result.Artists.Take(3))
                ArtistSuggestions.Add(artist);

            foreach (TrackDto track in result.Tracks.Take(3))
                TrackSuggestions.Add(track);

            HasResults = AlbumSuggestions.Count > 0 || ArtistSuggestions.Count > 0 || TrackSuggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation, it's expected when the user types quickly
        }
    }


    public void ClearSuggestions()
    {
        AlbumSuggestions.Clear();
        ArtistSuggestions.Clear();
        TrackSuggestions.Clear();
        HasResults = false;
    }
}
