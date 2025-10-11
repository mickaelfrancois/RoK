namespace Rok.Application.Messages;

public record PlaylistCreatedMessage(int PlaylistId, string Name);

public record PlaylistNameUpdatedMessage(int PlaylistId, string NewName);

public record PlaylistDeletedMessage(int PlaylistId);
