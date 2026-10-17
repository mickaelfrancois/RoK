using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Tells what editing the name, artist, year and genre of an album would do, without changing anything.
/// </summary>
/// <param name="albumId">Id of the album.</param>
/// <param name="name">Album name asked by the user.</param>
/// <param name="artistName">Artist name asked by the user.</param>
/// <param name="year">Year asked by the user, null to keep the current one.</param>
/// <param name="genreName">Genre name asked by the user, null to keep the current one.</param>
public class GetAlbumEditPreviewRequest(long albumId, string name, string artistName, int? year, string? genreName) : IRequest<Result<AlbumEditPreview>>
{
    public long AlbumId { get; } = albumId;

    public string Name { get; } = name;

    public string ArtistName { get; } = artistName;

    public int? Year { get; } = year;

    public string? GenreName { get; } = genreName;
}