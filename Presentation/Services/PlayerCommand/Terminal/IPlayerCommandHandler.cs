namespace Rok.Services.PlayerCommand.Terminal;

public interface IPlayerCommandHandler
{
    Task HandleAsync(string? arguments);
}