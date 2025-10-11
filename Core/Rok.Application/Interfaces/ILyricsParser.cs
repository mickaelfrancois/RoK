using Rok.Application.Dto.Lyrics;

namespace Rok.Application.Interfaces;

public interface ILyricsParser
{
    SyncLyricsModel Parse(string lyrics);
}