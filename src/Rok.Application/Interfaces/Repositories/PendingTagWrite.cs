using Rok.Domain.Enums;

namespace Rok.Application.Interfaces.Repositories;

/// <summary>A track whose file still has to receive the given fields from the database.</summary>
/// <param name="TrackId">Identifier of the track.</param>
/// <param name="Fields">Fields not written to the file yet.</param>
public sealed record PendingTagWrite(long TrackId, ETagWriteFields Fields);