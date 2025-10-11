using Rok.Shared.Enums;

namespace Rok.Application.Dto;

public class PlaylistFilterDto
{
    public SmartPlaylistEntity Entity { get; set; }

    public SmartPlaylistField Field { get; set; }

    public SmartPlaylistFieldType FieldType { get; set; }

    public SmartPlaylistOperator Operator { get; set; }

    public string? Value { get; set; }

    public string? Value2 { get; set; }


    public string Key
    {
        get
        {
            return $"{Entity}{Field}{Operator}";
        }
    }
}
