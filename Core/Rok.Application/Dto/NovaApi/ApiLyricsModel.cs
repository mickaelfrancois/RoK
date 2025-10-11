namespace Rok.Application.Dto.NovaApi;

public class ApiLyricsModel
{
    public enum EState { Draft, ServiceUpdate, Valid }

    public int? ID { get; set; }

    public string Artist { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Lyrics { get; set; } = string.Empty;

    public bool IsSynchronized { get; set; }
}