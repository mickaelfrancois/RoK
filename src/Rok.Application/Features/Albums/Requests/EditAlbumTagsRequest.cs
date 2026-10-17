using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Edits the name, artist, year and genre of an album in the database and in the tags of its files. The album keeps its id.
/// </summary>
/// <param name="albumId">Id of the album.</param>
/// <param name="name">Album name asked by the user.</param>
/// <param name="artistName">Artist name asked by the user.</param>
/// <param name="year">Year asked by the user, null to keep the current one.</param>
/// <param name="genreName">Genre name asked by the user, null to keep the current one.</param>
public class EditAlbumTagsRequest(long albumId, string name, string artistName, int? year, string? genreName) : IRequest<Result<AlbumTagWriteOutcome>>
{
    public long AlbumId { get; } = albumId;

    public string Name { get; } = name;

    public string ArtistName { get; } = artistName;

    public int? Year { get; } = year;

    public string? GenreName { get; } = genreName;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}