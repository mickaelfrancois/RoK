using Microsoft.UI.Dispatching;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Listening.Services;

public partial class ListeningPlaylistManager : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ListeningDataLoader _dataLoader;

    public RangeObservableCollection<TrackViewModel> Tracks { get; private set; } = [];
    public TrackViewModel? CurrentTrack { get; private set; }
    public ArtistViewModel? Artist { get; private set; }

    public int TrackCount => Tracks.Count;
    public long Duration => Tracks.Sum(c => c.Track.Duration);

    public event EventHandler? PlaylistChanged;
    public event EventHandler? CurrentTrackChanged;

    public ListeningPlaylistManager(DispatcherQueue dispatcherQueue, ListeningDataLoader dataLoader)
    {
        _dispatcherQueue = dispatcherQueue;
        _dataLoader = dataLoader;
    }

    public void LoadTracksList(List<TrackDto> tracks)
    {
        Tracks.Clear();

        if (tracks != null)
            Tracks.AddRange(_dataLoader.CreateTracksViewModels(tracks));

        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(Duration));
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetCurrentTrackAsync(TrackDto? track)
    {
        if (track == null)
        {
            ClearData();
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (TrackViewModel trackViewModel in Tracks.Where(c => c.Listening))
                trackViewModel.Listening = false;

            CurrentTrack = Tracks.FirstOrDefault(c => c.Track.Id == track.Id);

            if (CurrentTrack != null)
            {
                CurrentTrack.Listening = true;
                OnPropertyChanged(nameof(CurrentTrack));
            }
        });

        await LoadArtistIfNeededAsync(track);
        CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadArtistIfNeededAsync(TrackDto track)
    {
        if (Artist?.Artist.Id != track.ArtistId && track.ArtistId.HasValue)
        {
            ArtistViewModel? newArtist = await _dataLoader.LoadArtistAsync(track.ArtistId.Value);

            if (newArtist != null)
            {
                Artist = newArtist;

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OnPropertyChanged(nameof(Artist));
                });
            }
        }
    }

    private void ClearData()
    {
        CurrentTrack = null;
        Artist = null;

        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(Artist));
    }
}