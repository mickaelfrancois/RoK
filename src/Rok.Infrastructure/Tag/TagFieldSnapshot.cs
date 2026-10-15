using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Raw editable fields read from a file, compared before and after a tag write.
/// </summary>
/// <param name="Title">Title as read.</param>
/// <param name="Track">Track number.</param>
/// <param name="TrackCount">Track total.</param>
/// <param name="Disc">Disc number.</param>
/// <param name="DiscCount">Disc total.</param>
/// <param name="Bpm">Beats per minute.</param>
/// <param name="Composers">Composers in canonical form.</param>
internal sealed record TagFieldSnapshot(
    string? Title,
    uint Track,
    uint TrackCount,
    uint Disc,
    uint DiscCount,
    uint Bpm,
    string Composers)
{
    /// <summary>
    /// Gives the snapshot a verified copy must have once the changes are applied.
    /// </summary>
    /// <param name="changes">Changes written to the copy.</param>
    /// <returns>The expected snapshot.</returns>
    public TagFieldSnapshot WithChanges(TagChanges changes)
    {
        return this with
        {
            Title = changes.Title ?? Title,
            Track = (uint?)changes.TrackNumber ?? Track,
            Disc = (uint?)changes.Disc ?? Disc,
            Bpm = (uint?)changes.Bpm ?? Bpm,
            Composers = changes.Composers is null ? Composers : TagFieldReader.CanonicalComposers(changes.Composers)
        };
    }
}