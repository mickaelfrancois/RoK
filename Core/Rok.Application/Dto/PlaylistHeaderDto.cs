namespace Rok.Application.Dto;

public class PlaylistHeaderDto : PlaylistHeaderEntity
{
    public List<PlaylistGroupDto> Groups { get; set; } = [];
}
