using Rok.Application.Player;

namespace Rok.Services;

public interface IPlayerCommandHandler
{
    void Handle(string? arguments);
}

public sealed class PlayerCommandHandler(IPlayerService playerService, ILogger<PlayerCommandHandler> logger) : IPlayerCommandHandler
{
    public void Handle(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        string command = GetCommand(arguments);

        logger.LogInformation("Handling player command: {Command}", command);

        switch (command)
        {
            case "play":
                playerService.Play();
                break;
            case "pause":
                playerService.Pause();
                break;
            case "next":
                playerService.Next();
                break;
            case "prev":
            case "previous":
                playerService.Previous();
                break;
            case "stop":
                playerService.Stop(true);
                break;
        }
    }

    private static string GetCommand(string arguments)
    {
        string[] parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string firstPart = parts[0].Trim('"');
        bool isRok = firstPart.Equals("rok", StringComparison.OrdinalIgnoreCase);
        bool isExecutablePath = firstPart.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        int skip = (isRok || isExecutablePath) ? 1 : 0;

        if (parts.Length <= skip)
            return string.Empty;

        return parts[skip].ToLowerInvariant();
    }
}