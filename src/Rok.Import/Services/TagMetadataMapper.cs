using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Import.Services;

/// <summary>
/// How tag-derived values are written onto an entity.
/// </summary>
public enum MetadataWritePolicy
{
    /// <summary>
    /// Assign every field from the tag verbatim, including clearing a value to
    /// <see langword="null"/>/0 when the tag carries none. Used when (re)building a row at import.
    /// </summary>
    Mirror,

    /// <summary>
    /// Assign a field only when the tag actually provides a value; never blank an existing
    /// value. Used when refreshing an already-populated row from the tag.
    /// </summary>
    FillFromTag
}

/// <summary>
/// Single source of truth for mapping the extended audio-tag metadata read into a
/// <see cref="TrackFile"/> onto the track and album entities. Shared by the import pipeline
/// (<see cref="MetadataWritePolicy.Mirror"/>) and the maintenance refresher
/// (<see cref="MetadataWritePolicy.FillFromTag"/>) so the field set lives in one place.
/// </summary>
public static class TagMetadataMapper
{
    /// <summary>Applies the extended track-level tag metadata. Returns whether anything changed.</summary>
    public static bool ApplyTrackMetadata(TrackEntity track, TrackFile file, MetadataWritePolicy policy)
    {
        bool changed = false;

        changed |= SetNullableInt(file.Disc, () => track.Disc, value => track.Disc = value, policy);
        changed |= SetNullableInt(file.Bpm, () => track.Bpm, value => track.Bpm = value, policy);
        changed |= SetText(file.Composers, () => track.Composers, value => track.Composers = value, policy);
        changed |= SetInt(file.SampleRate, () => track.SampleRate, value => track.SampleRate = value, policy);
        changed |= SetInt(file.BitsPerSample, () => track.BitsPerSample, value => track.BitsPerSample = value, policy);
        changed |= SetInt(file.Channels, () => track.Channels, value => track.Channels = value, policy);
        changed |= SetNullableDouble(file.ReplayGainTrackGain, () => track.ReplayGainTrackGain, value => track.ReplayGainTrackGain = value, policy);
        changed |= SetNullableDouble(file.ReplayGainTrackPeak, () => track.ReplayGainTrackPeak, value => track.ReplayGainTrackPeak = value, policy);

        return changed;
    }

    /// <summary>Applies the extended album-level tag metadata. Returns whether anything changed.</summary>
    public static bool ApplyAlbumMetadata(AlbumEntity album, TrackFile file, MetadataWritePolicy policy)
    {
        bool changed = false;

        changed |= SetNullableInt(file.DiscCount, () => album.DiscCount, value => album.DiscCount = value, policy);
        changed |= SetNullableDouble(file.ReplayGainAlbumGain, () => album.ReplayGainAlbumGain, value => album.ReplayGainAlbumGain = value, policy);
        changed |= SetNullableDouble(file.ReplayGainAlbumPeak, () => album.ReplayGainAlbumPeak, value => album.ReplayGainAlbumPeak = value, policy);
        changed |= SetText(file.MusicBrainzReleaseType, () => album.MusicBrainzReleaseType, value => album.MusicBrainzReleaseType = value, policy);
        changed |= SetText(file.MusicBrainzReleaseCountry, () => album.MusicBrainzReleaseCountry, value => album.MusicBrainzReleaseCountry = value, policy);
        changed |= SetText(file.MusicbrainzAlbumID, () => album.MusicBrainzID, value => album.MusicBrainzID = value, policy);

        return changed;
    }

    private static bool SetNullableInt(int? tagValue, Func<int?> get, Action<int?> set, MetadataWritePolicy policy)
    {
        if (policy == MetadataWritePolicy.FillFromTag && !tagValue.HasValue)
            return false;

        if (tagValue == get())
            return false;

        set(tagValue);
        return true;
    }

    private static bool SetInt(int tagValue, Func<int> get, Action<int> set, MetadataWritePolicy policy)
    {
        if (policy == MetadataWritePolicy.FillFromTag && tagValue <= 0)
            return false;

        if (tagValue == get())
            return false;

        set(tagValue);
        return true;
    }

    private static bool SetNullableDouble(double? tagValue, Func<double?> get, Action<double?> set, MetadataWritePolicy policy)
    {
        if (policy == MetadataWritePolicy.FillFromTag && !tagValue.HasValue)
            return false;

        if (tagValue == get())
            return false;

        set(tagValue);
        return true;
    }

    private static bool SetText(string? tagValue, Func<string?> get, Action<string?> set, MetadataWritePolicy policy)
    {
        string? normalized = string.IsNullOrEmpty(tagValue) ? null : tagValue;

        if (policy == MetadataWritePolicy.FillFromTag && normalized is null)
            return false;

        if (normalized == get())
            return false;

        set(normalized);
        return true;
    }
}
