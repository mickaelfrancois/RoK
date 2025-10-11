using Rok.Application.Features.Playlists.Command;
using System.Text.Json;

namespace Rok.Application.Mapping;

public static class PlaylistHeadeDtoMapping
{
    public static PlaylistHeaderDto Map(PlaylistHeaderEntity entity)
    {
        List<PlaylistGroupDto> groups = (entity.Type == 0 && !string.IsNullOrWhiteSpace(entity.GroupsJson))
            ? (JsonSerializer.Deserialize<List<PlaylistGroupDto>>(entity.GroupsJson) ?? new List<PlaylistGroupDto>())
            : new List<PlaylistGroupDto>();

        return new PlaylistHeaderDto
        {
            Id = entity.Id,
            CreatDate = entity.CreatDate,
            EditDate = entity.EditDate,
            Name = entity.Name,
            Picture = entity.Picture,
            TrackCount = entity.TrackCount,
            Duration = entity.Duration,
            TrackMaximum = entity.TrackMaximum,
            DurationMaximum = entity.DurationMaximum,
            GroupsJson = entity.GroupsJson,
            Type = entity.Type,
            Groups = groups
        };
    }

    public static PlaylistHeaderEntity Map(CreatePlaylistCommand command)
    {
        return new PlaylistHeaderEntity
        {
            Name = command.Name,
            Picture = command.Picture,
            TrackMaximum = command.TrackMaximum,
            DurationMaximum = command.DurationMaximum,
            GroupsJson = command.Groups is { Count: > 0 } ? JsonSerializer.Serialize(command.Groups) : string.Empty,
            Type = command.Type
        };
    }

    public static PlaylistHeaderEntity Map(UpdatePlaylistCommand command)
    {
        return new PlaylistHeaderEntity
        {
            Id = command.Id,
            Name = command.Name,
            Picture = command.Picture,
            TrackCount = command.TrackCount,
            Duration = command.Duration,
            TrackMaximum = command.TrackMaximum,
            DurationMaximum = command.DurationMaximum,
            GroupsJson = command.Groups is { Count: > 0 } ? JsonSerializer.Serialize(command.Groups) : string.Empty,
            Type = command.Type
        };
    }

    public static CreatePlaylistCommand Map(PlaylistHeaderDto dto)
    {
        return new CreatePlaylistCommand
        {
            Name = dto.Name,
            Picture = dto.Picture,
            TrackMaximum = dto.TrackMaximum,
            DurationMaximum = dto.DurationMaximum,
            Type = dto.Type,
            Groups = dto.Groups ?? new List<PlaylistGroupDto>()
        };
    }

    public static UpdatePlaylistCommand MapToUpdatePlaylistCommand(PlaylistHeaderDto dto)
    {
        return new UpdatePlaylistCommand
        {
            Id = dto.Id,
            Name = dto.Name,
            Picture = dto.Picture,
            Duration = dto.Duration,
            TrackCount = dto.TrackCount,
            TrackMaximum = dto.TrackMaximum,
            DurationMaximum = dto.DurationMaximum,
            Type = dto.Type,
            Groups = dto.Groups ?? new List<PlaylistGroupDto>()
        };
    }
}
