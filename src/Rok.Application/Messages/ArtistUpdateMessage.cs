using Rok.Shared.Enums;

namespace Rok.Application.Messages;

public class ArtistUpdateMessage(long id, ActionType action)
{
    public long Id { get; init; } = id;

    public ActionType Action { get; init; } = action;
}
