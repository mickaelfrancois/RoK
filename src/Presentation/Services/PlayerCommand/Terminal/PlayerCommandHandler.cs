using Rok.Application.PlayerCommand.Terminal;

namespace Rok.Services.PlayerCommand.Terminal;

/// <summary>
/// Handles player commands such as play, pause, toggle, next, and previous by processing command strings and invoking
/// the corresponding actions on the player command service.
/// </summary>
/// <remarks>Commands are case-insensitive and may be issued with or without an executable path prefix. This
/// handler interprets the command string and delegates execution to the appropriate method on the command
/// service.</remarks>
/// <param name="commandService">The service responsible for executing player commands such as play, pause, toggle, next, and previous.</param>
/// <param name="logger">The logger used to record information about command handling operations.</param>
public sealed class PlayerCommandHandler(IPlayerCommandService commandService, ILogger<PlayerCommandHandler> logger) : IPlayerCommandHandler
{
    public async Task HandleAsync(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        string[] args = CliArgumentParser.Parse(arguments);
        if (args.Length == 0)
            return;

        string command = args[0];
        string[] commandArgs = args.Skip(1).ToArray();

        logger.LogInformation("Handling player command: {Command} {Args}", command, commandArgs);

        switch (command)
        {
            case "play":
                commandService.Play();
                break;

            case "pause":
                commandService.Pause();
                break;

            case "toggle":
                commandService.Toggle();
                break;

            case "next":
                commandService.Next();
                break;

            case "prev":
            case "previous":
                commandService.Previous();
                break;

            case "mute":
                commandService.ToggleMute();
                break;

            case "listen":
                await HandleListenAsync(commandArgs);
                break;

            case "volume":
                if (double.TryParse(commandArgs[0], out double volume))
                    commandService.SetVolume(volume);
                break;
        }
    }

    private async Task HandleListenAsync(string[] commandArgs)
    {
        if (commandArgs.Length <= 1)
            return;

        switch (commandArgs[0].ToLowerInvariant())
        {
            case "album":
                await commandService.ListenAlbumAsync(commandArgs[1]);
                break;

            case "artist":
                await commandService.ListenArtistAsync(commandArgs[1]);
                break;

            case "genre":
                await commandService.ListenGenreAsync(commandArgs[1]);
                break;

            case "playlist":
                await commandService.ListenPlaylistAsync(commandArgs[1]);
                break;
        }
    }
}