using System.Text.Json;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Mapping;
using Rok.Domain.Entities;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Mapping;

public class PlaylistHeaderDtoMappingTests
{
    [Fact(DisplayName = "Map entity to dto should deserialize Groups when type is Smart and Groups JSON is present")]
    public void MapEntityToDto_ShouldDeserializeGroups_WhenSmartTypeWithGroupsJson()
    {
        // Arrange
        List<PlaylistGroupDto> groups = new() { new() { Name = "g1" }, new() { Name = "g2" } };
        PlaylistHeaderEntity entity = new()
        {
            Id = 1,
            Name = "MySmart",
            Type = (int)PlaylistType.Smart,
            GroupsJson = JsonSerializer.Serialize(groups)
        };

        // Act
        PlaylistHeaderDto dto = PlaylistHeadeDtoMapping.Map(entity);

        // Assert
        Assert.Equal(2, dto.Groups.Count);
        Assert.Equal("g1", dto.Groups[0].Name);
    }

    [Fact(DisplayName = "Map entity to dto should leave Groups empty when type is not Smart")]
    public void MapEntityToDto_ShouldLeaveGroupsEmpty_WhenNotSmartType()
    {
        // Arrange
        PlaylistHeaderEntity entity = new()
        {
            Id = 1,
            Name = "Plain",
            Type = (int)PlaylistType.Classic,
            GroupsJson = JsonSerializer.Serialize(new List<PlaylistGroupDto> { new() { Name = "g1" } })
        };

        // Act
        PlaylistHeaderDto dto = PlaylistHeadeDtoMapping.Map(entity);

        // Assert
        Assert.Empty(dto.Groups);
    }

    [Fact(DisplayName = "Map entity to dto should leave Groups empty when GroupsJson is empty even for Smart type")]
    public void MapEntityToDto_ShouldLeaveGroupsEmpty_WhenGroupsJsonIsEmpty()
    {
        // Arrange
        PlaylistHeaderEntity entity = new()
        {
            Id = 1,
            Name = "Empty",
            Type = (int)PlaylistType.Smart,
            GroupsJson = string.Empty
        };

        // Act
        PlaylistHeaderDto dto = PlaylistHeadeDtoMapping.Map(entity);

        // Assert
        Assert.Empty(dto.Groups);
    }

    [Fact(DisplayName = "Map create command to entity should serialize Groups to JSON when present")]
    public void MapCreateCommandToEntity_ShouldSerializeGroupsToJson()
    {
        // Arrange
        CreatePlaylistRequest command = new()
        {
            Name = "MySmart",
            Type = (int)PlaylistType.Smart,
            TrackMaximum = 10,
            DurationMaximum = 3600,
            Groups = new() { new() { Name = "g1" } }
        };

        // Act
        PlaylistHeaderEntity entity = PlaylistHeadeDtoMapping.Map(command);

        // Assert
        Assert.Equal("MySmart", entity.Name);
        Assert.Equal(10, entity.TrackMaximum);
        Assert.Equal(3600, entity.DurationMaximum);
        Assert.Contains("g1", entity.GroupsJson);
    }

    [Fact(DisplayName = "Map create command to entity should leave GroupsJson empty when no groups are provided")]
    public void MapCreateCommandToEntity_ShouldLeaveGroupsJsonEmpty_WhenNoGroups()
    {
        // Arrange
        CreatePlaylistRequest command = new()
        {
            Name = "Plain",
            Type = (int)PlaylistType.Classic,
            Groups = new()
        };

        // Act
        PlaylistHeaderEntity entity = PlaylistHeadeDtoMapping.Map(command);

        // Assert
        Assert.Equal(string.Empty, entity.GroupsJson);
    }

    [Fact(DisplayName = "MapToUpdatePlaylistRequest should preserve dto fields including the Groups collection")]
    public void MapToUpdatePlaylistRequest_ShouldPreserveFields()
    {
        // Arrange
        List<PlaylistGroupDto> groups = new() { new() { Name = "g1" } };
        PlaylistHeaderDto dto = new()
        {
            Id = 1,
            Name = "MySmart",
            TrackMaximum = 10,
            DurationMaximum = 3600,
            Type = (int)PlaylistType.Smart,
            Groups = groups
        };

        // Act
        UpdatePlaylistRequest command = PlaylistHeadeDtoMapping.MapToUpdatePlaylistRequest(dto);

        // Assert
        Assert.Equal(1, command.Id);
        Assert.Equal("MySmart", command.Name);
        Assert.Equal(10, command.TrackMaximum);
        Assert.Equal(3600, command.DurationMaximum);
        Assert.Single(command.Groups);
    }
}