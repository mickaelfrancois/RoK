using Rok.Application.Dto;

namespace Rok.Application.Interfaces;

public interface IDiscordRichPresenceService
{
    void ClearPresence();
    void Dispose();
    void Initialize();
    void UpdatePresence(string trackTitle, string artistName, string albumName, TimeSpan elapsed, TimeSpan duration);
    void UpdateRadioStation(RadioStationDto station);
    void UpdateRadioMetadata(string streamTitle);
}