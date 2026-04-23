
namespace Rok.Infrastructure.Social;

public interface IDiscordRichPresenceService
{
    void ClearPresence();
    void Dispose();
    void Initialize();
    void UpdatePresence(string trackTitle, string artistName, string albumName, TimeSpan elapsed, TimeSpan duration);
}