using Rok.Application.Features.Playlists.Command;

namespace Rok.Services;

public class PlaylistsSeed(IMediator _mediator, ResourceLoader _resourceLoader)
{
    public async Task SeedAsync()
    {
        await InitRadioPlaylistsAsync();
        await InitBestofPlaylistsAsync();
        await InitAlbumsOfTheYearPlaylistsAsync();
    }

    private async Task InitRadioPlaylistsAsync()
    {
        CreatePlaylistCommand playlist = new()
        {
            Name = _resourceLoader.GetString("defaultRadioPlaylistName"),
            Type = (int)PlaylistType.Smart,
            TrackMaximum = 100,
            DurationMaximum = 3600
        };

        PlaylistGroupDto group1 = new()
        {
            Name = "New tracks",
            Position = 0,
            TrackCount = 5,
        };

        group1.Filters.Add(new PlaylistFilterDto
        {
            Entity = SmartPlaylistEntity.Tracks,
            FieldType = SmartPlaylistFieldType.Day,
            Operator = SmartPlaylistOperator.GreaterThan,
            Field = SmartPlaylistField.CreatDate,
            Value = "90"
        });
        group1.SortBy = SmartPlaylistSelectBy.LeastPlayed;


        PlaylistGroupDto group2 = new()
        {
            Name = "Best of",
            Position = 1,
            TrackCount = 1,
        };

        group2.Filters.Add(new PlaylistFilterDto
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.Score,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.GreaterThan,
            Value = "0"
        });
        group2.SortBy = SmartPlaylistSelectBy.LeastPlayed;

        playlist.Groups.AddRange(group1, group2);

        await _mediator.SendMessageAsync(playlist);
    }

    private async Task InitBestofPlaylistsAsync()
    {
        CreatePlaylistCommand playlist = new()
        {
            Name = _resourceLoader.GetString("defaultBestofPlaylistName"),
            Type = (int)PlaylistType.Smart,
            TrackMaximum = 100,
            DurationMaximum = 3600
        };

        PlaylistGroupDto group1 = new()
        {
            Name = "Best of least played",
            Position = 0,
            TrackCount = 10,
        };

        group1.Filters.Add(new PlaylistFilterDto
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.Score,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.GreaterThan,
            Value = "0"
        });
        group1.SortBy = SmartPlaylistSelectBy.LeastPlayed;


        PlaylistGroupDto group2 = new()
        {
            Name = "Best of most played",
            Position = 1,
            TrackCount = 2,
        };

        group2.Filters.Add(new PlaylistFilterDto
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.Score,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.GreaterThan,
            Value = "0"
        });
        group2.SortBy = SmartPlaylistSelectBy.MostPlayed;

        playlist.Groups.AddRange(group1, group2);

        await _mediator.SendMessageAsync(playlist);
    }

    private async Task InitAlbumsOfTheYearPlaylistsAsync()
    {
        CreatePlaylistCommand playlist = new()
        {
            Name = _resourceLoader.GetString("defaultAlbumOfTheYearfPlaylistName"),
            Type = (int)PlaylistType.Smart,
            TrackMaximum = 100,
            DurationMaximum = 3600
        };

        PlaylistGroupDto group1 = new()
        {
            Name = "Album of the year",
            Position = 0,
            TrackCount = 10,
        };

        group1.Filters.Add(new PlaylistFilterDto
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.Year,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.Equals,
            Value = "2025"
        });
        group1.SortBy = SmartPlaylistSelectBy.Random;

        playlist.Groups.Add(group1);

        await _mediator.SendMessageAsync(playlist);
    }
}
