namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// Pure rules that decide the final values of the kept album when two albums are merged.
/// </summary>
public static class AlbumMergeRules
{
    /// <summary>
    /// Merges the merged album (<paramref name="source"/>) into the kept album (<paramref name="target"/>).
    /// Empty fields of the kept album are filled from the merged one; a non empty field is never overwritten
    /// and a difference is listed as a conflict. Counters derived from tracks are left to the persistence step.
    /// </summary>
    /// <param name="target">Album that stays.</param>
    /// <param name="source">Album that disappears.</param>
    public static AlbumMergeResult Merge(AlbumEntity target, AlbumEntity source)
    {
        List<AlbumMergeFieldConflict> conflicts = [];

        AlbumEntity merged = new()
        {
            Id = target.Id,
            Name = target.Name,
            ArtistId = target.ArtistId,
            AlbumPath = target.AlbumPath,
            IsLive = target.IsLive,
            IsBestOf = target.IsBestOf,
            IsCompilation = target.IsCompilation,
            CreatDate = target.CreatDate,
            EditDate = target.EditDate,
            PictureDominantColor = target.PictureDominantColor,
            TrackCount = target.TrackCount,
            Duration = target.Duration,
            DiscCount = target.DiscCount,
            GetMetaDataLastAttempt = target.GetMetaDataLastAttempt,
            ReplayGainAlbumGain = target.ReplayGainAlbumGain,
            ReplayGainAlbumPeak = target.ReplayGainAlbumPeak,

            MusicBrainzID = Text(nameof(AlbumEntity.MusicBrainzID), target.MusicBrainzID, source.MusicBrainzID, conflicts),
            ReleaseGroupMusicBrainzID = Text(nameof(AlbumEntity.ReleaseGroupMusicBrainzID), target.ReleaseGroupMusicBrainzID, source.ReleaseGroupMusicBrainzID, conflicts),
            AudioDbID = Text(nameof(AlbumEntity.AudioDbID), target.AudioDbID, source.AudioDbID, conflicts),
            AudioDbArtistID = Text(nameof(AlbumEntity.AudioDbArtistID), target.AudioDbArtistID, source.AudioDbArtistID, conflicts),
            AllMusicID = Text(nameof(AlbumEntity.AllMusicID), target.AllMusicID, source.AllMusicID, conflicts),
            DiscogsID = Text(nameof(AlbumEntity.DiscogsID), target.DiscogsID, source.DiscogsID, conflicts),
            MusicMozID = Text(nameof(AlbumEntity.MusicMozID), target.MusicMozID, source.MusicMozID, conflicts),
            LyricWikiID = Text(nameof(AlbumEntity.LyricWikiID), target.LyricWikiID, source.LyricWikiID, conflicts),
            GeniusID = Text(nameof(AlbumEntity.GeniusID), target.GeniusID, source.GeniusID, conflicts),
            WikipediaID = Text(nameof(AlbumEntity.WikipediaID), target.WikipediaID, source.WikipediaID, conflicts),
            WikidataID = Text(nameof(AlbumEntity.WikidataID), target.WikidataID, source.WikidataID, conflicts),
            AmazonID = Text(nameof(AlbumEntity.AmazonID), target.AmazonID, source.AmazonID, conflicts),
            Wikipedia = Text(nameof(AlbumEntity.Wikipedia), target.Wikipedia, source.Wikipedia, conflicts),
            Biography = Text(nameof(AlbumEntity.Biography), target.Biography, source.Biography, conflicts),
            LastFmUrl = Text(nameof(AlbumEntity.LastFmUrl), target.LastFmUrl, source.LastFmUrl, conflicts),
            Label = Text(nameof(AlbumEntity.Label), target.Label, source.Label, conflicts),
            Sales = Text(nameof(AlbumEntity.Sales), target.Sales, source.Sales, conflicts),
            ReleaseFormat = Text(nameof(AlbumEntity.ReleaseFormat), target.ReleaseFormat, source.ReleaseFormat, conflicts),
            MusicBrainzReleaseType = Text(nameof(AlbumEntity.MusicBrainzReleaseType), target.MusicBrainzReleaseType, source.MusicBrainzReleaseType, conflicts),
            MusicBrainzReleaseCountry = Text(nameof(AlbumEntity.MusicBrainzReleaseCountry), target.MusicBrainzReleaseCountry, source.MusicBrainzReleaseCountry, conflicts),

            Year = Year(target.Year, source.Year, conflicts),
            GenreId = Reference(target.GenreId, source.GenreId, conflicts),
            ReleaseDate = Date(target.ReleaseDate, source.ReleaseDate, conflicts),

            IsFavorite = target.IsFavorite || source.IsFavorite,
            IsLock = target.IsLock || source.IsLock,
            ListenCount = target.ListenCount + source.ListenCount,
            LastListen = MaxDate(target.LastListen, source.LastListen),
        };

        return new AlbumMergeResult(merged, conflicts);
    }

    private static string? Text(string field, string? target, string? source, List<AlbumMergeFieldConflict> conflicts)
    {
        if (string.IsNullOrWhiteSpace(source))
            return target;

        if (string.IsNullOrWhiteSpace(target))
            return source;

        if (!string.Equals(target.Trim(), source.Trim(), StringComparison.Ordinal))
            conflicts.Add(new AlbumMergeFieldConflict(field, target, source));

        return target;
    }

    private static int? Year(int? target, int? source, List<AlbumMergeFieldConflict> conflicts)
    {
        bool targetEmpty = target is null or 0;
        bool sourceEmpty = source is null or 0;

        if (sourceEmpty)
            return target;

        if (targetEmpty)
            return source;

        if (target != source)
            conflicts.Add(new AlbumMergeFieldConflict(nameof(AlbumEntity.Year), target.ToString(), source.ToString()));

        return target;
    }

    private static long? Reference(long? target, long? source, List<AlbumMergeFieldConflict> conflicts)
    {
        bool targetEmpty = target is null or 0;
        bool sourceEmpty = source is null or 0;

        if (sourceEmpty)
            return target;

        if (targetEmpty)
            return source;

        if (target != source)
            conflicts.Add(new AlbumMergeFieldConflict(nameof(AlbumEntity.GenreId), target.ToString(), source.ToString()));

        return target;
    }

    private static DateTime? Date(DateTime? target, DateTime? source, List<AlbumMergeFieldConflict> conflicts)
    {
        if (source is null)
            return target;

        if (target is null)
            return source;

        if (target != source)
            conflicts.Add(new AlbumMergeFieldConflict(nameof(AlbumEntity.ReleaseDate), target.ToString(), source.ToString()));

        return target;
    }

    private static DateTime? MaxDate(DateTime? left, DateTime? right)
    {
        if (left is null)
            return right;

        if (right is null)
            return left;

        return left > right ? left : right;
    }
}

/// <summary>
/// Outcome of <see cref="AlbumMergeRules.Merge"/>.
/// </summary>
/// <param name="Merged">Final values of the kept album.</param>
/// <param name="Conflicts">Fields filled on both albums with different values.</param>
public sealed record AlbumMergeResult(AlbumEntity Merged, IReadOnlyList<AlbumMergeFieldConflict> Conflicts);