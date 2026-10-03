using Rok.Application.Dto;
using Rok.Domain.Entities;

namespace Rok.MetadataTool;

/// <summary>Selection of the tracks a Mix scan analyses.</summary>
internal static class MixScanTracks
{
    /// <summary>Copies the fields the Mix analysis and the scan filter read, the same way the app maps a track.</summary>
    public static TrackDto ToDto(TrackEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        MusicFile = entity.MusicFile,
        FileDate = entity.FileDate,
        Size = entity.Size,
        Bpm = entity.Bpm,
        IsLive = entity.IsLive,
        IsAlbumLive = entity.IsAlbumLive,
    };

    /// <summary>Live tracks are never mixed, so they are never analysed.</summary>
    public static bool IsAnalysable(TrackDto track) => !track.IsLive && !track.IsAlbumLive;

    /// <summary>Keeps the analysable tracks in identifier order, then applies the limit.</summary>
    public static IReadOnlyList<TrackDto> Select(IEnumerable<TrackDto> all, int? limit)
    {
        var selected = all.Where(IsAnalysable).OrderBy(t => t.Id);

        return limit is { } max ? selected.Take(max).ToList() : selected.ToList();
    }
}