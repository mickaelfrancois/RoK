using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Genres.Command;
using Rok.Application.Features.Statistics;
using Rok.Application.Features.Statistics.Query;
using Rok.Application.Features.Tracks.Command;

namespace Rok.Logic.ViewModels.Statistics;

public partial class StatisticsViewModel(IMediator mediator) : ObservableObject
{
    public UserStatisticsDto Current { get; private set; } = new();

    public string FormatWithThousands(long value)
    {
        return value.ToString("N0");
    }

    public string TotalDuration
    {
        get
        {
            if (Current.TotalDurationSeconds <= 0)
                return "0:00:00";

            TimeSpan ts = TimeSpan.FromSeconds(Current.TotalDurationSeconds);
            int days = ts.Days;
            int hours = ts.Hours;
            int minutes = ts.Minutes;

            return string.Create(CultureInfo.InvariantCulture, $"{days}:{hours:D2}:{minutes:D2}");
        }
    }

    public string TotalSize
    {
        get
        {
            if (Current.TotalSizeBytes <= 0)
                return "0";

            const long OneMB = 1_000_000L;
            const long OneGB = 1_000_000_000L;

            if (Current.TotalSizeBytes >= OneGB)
            {
                double gb = Current.TotalSizeBytes / (double)OneGB;
                return $"{gb.ToString("0.##", CultureInfo.InvariantCulture)} GB";
            }

            double mb = Current.TotalSizeBytes / (double)OneMB;
            long mbRounded = (long)Math.Round(mb);
            return $"{FormatWithThousands(mbRounded)} MB";
        }
    }

    public long TotalSyncLyrics { get; set; }

    public long TotalRawLyrics { get; set; }

    public IReadOnlyList<RankedTopItem> TopGenres { get; set; } = [];

    public IReadOnlyList<RankedTopItem> TopArtists { get; set; } = [];

    public IReadOnlyList<RankedTopItem> TopAlbums { get; set; } = [];

    public IReadOnlyList<RankedTopItem> TopTracks { get; set; } = [];

    public async Task LoadAsync()
    {
        Current = await mediator.SendMessageAsync(new GetStatisticsQuery());

        TopTracks = (Current.TopTracks ?? new List<TopItem>())
           .Select((t, i) => new RankedTopItem { Rank = i + 1, Id = t.Id, Name = t.Name, ListenCount = t.ListenCount })
           .ToList();

        TopAlbums = (Current.TopAlbums ?? new List<TopItem>())
              .Select((t, i) => new RankedTopItem { Rank = i + 1, Id = t.Id, Name = t.Name, ListenCount = t.ListenCount })
              .ToList();

        TopArtists = (Current.TopArtists ?? new List<TopItem>())
          .Select((t, i) => new RankedTopItem { Rank = i + 1, Id = t.Id, Name = t.Name, ListenCount = t.ListenCount })
          .ToList();

        TopGenres = (Current.TopGenres ?? new List<TopItem>())
          .Select((t, i) => new RankedTopItem { Rank = i + 1, Id = t.Id, Name = t.Name, ListenCount = t.ListenCount })
          .ToList();

        LyricsStatisticsDto lyrics = await mediator.SendMessageAsync(new GetLyricsStatisticsQuery());
        TotalRawLyrics = lyrics.TotalRawLyrics;
        TotalSyncLyrics = lyrics.TotalSyncLyrics;

        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(TopTracks));
        OnPropertyChanged(nameof(TopAlbums));
        OnPropertyChanged(nameof(TopArtists));
        OnPropertyChanged(nameof(TopGenres));
        OnPropertyChanged(nameof(TotalDuration));
        OnPropertyChanged(nameof(TotalSize));
        OnPropertyChanged(nameof(TotalRawLyrics));
        OnPropertyChanged(nameof(TotalSyncLyrics));
    }


    [RelayCommand]
    private async Task ResetListenCountAsync()
    {
        await mediator.SendMessageAsync(new ResetGenreListenCountCommand());
        await mediator.SendMessageAsync(new ResetArtistListenCountCommand());
        await mediator.SendMessageAsync(new ResetAlbumListenCountCommand());
        await mediator.SendMessageAsync(new ResetTrackListenCountCommand());

        await LoadAsync();
    }
}

public record RankedTopItem
{
    public int Rank { get; init; }
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ListenCount { get; init; }
}