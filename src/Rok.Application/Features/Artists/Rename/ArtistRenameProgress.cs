namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Progress of the tag writing of an artist rename.
/// </summary>
/// <param name="Done">Number of files written so far.</param>
/// <param name="Total">Number of files to write.</param>
public sealed record ArtistRenameProgress(int Done, int Total);