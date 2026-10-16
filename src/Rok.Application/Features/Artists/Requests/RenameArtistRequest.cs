using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

/// <summary>
/// Renames an artist in the database and in the tags of its files. The artist keeps its id.
/// </summary>
/// <param name="artistId">Id of the artist.</param>
/// <param name="newName">Name asked by the user. To resume an interrupted rename, the current name of the artist.</param>
public class RenameArtistRequest(long artistId, string newName) : IRequest<Result<ArtistRenameOutcome>>
{
    public long ArtistId { get; } = artistId;

    public string NewName { get; } = newName;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}