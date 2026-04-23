namespace Rok.Application.Messages;

/// <summary>
/// Media control commands sent by System Media Transport Controls (SMTC) from Bluetooth headsets.
/// </summary>
public sealed record MediaControlCommandMessage
{
    public enum CommandType
    {
        Play,
        Pause,
        Next,
        Previous,
        Stop
    }

    public CommandType Command { get; init; }

    public MediaControlCommandMessage(CommandType command)
    {
        Command = command;
    }
}
