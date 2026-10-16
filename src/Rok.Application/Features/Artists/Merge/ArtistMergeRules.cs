using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Pure rules that decide the final values of the kept artist when two artists are merged.
/// </summary>
public static class ArtistMergeRules
{
    /// <summary>
    /// Merges the merged artist (<paramref name="source"/>) into the kept artist (<paramref name="target"/>).
    /// Empty fields of the kept artist are filled from the merged one; a non empty field is never overwritten
    /// and a difference is listed as a conflict. Counters derived from tracks are left to the persistence step.
    /// </summary>
    /// <param name="target">Artist that stays.</param>
    /// <param name="source">Artist that disappears.</param>
    /// <param name="targetHasPicture">True when the kept artist has a picture; otherwise the merged artist's dominant color is taken.</param>
    public static ArtistMergeResult Merge(ArtistEntity target, ArtistEntity source, bool targetHasPicture)
    {
        List<ArtistMergeFieldConflict> conflicts = [];

        ArtistEntity merged = new()
        {
            Id = target.Id,
            Name = target.Name,
            CreatDate = target.CreatDate,
            EditDate = target.EditDate,
            GetMetaDataLastAttempt = target.GetMetaDataLastAttempt,
            RenamePendingFrom = target.RenamePendingFrom,
            YearMini = target.YearMini,
            YearMaxi = target.YearMaxi,
            TrackCount = target.TrackCount,
            AlbumCount = target.AlbumCount,
            LiveCount = target.LiveCount,
            CompilationCount = target.CompilationCount,
            BestofCount = target.BestofCount,
            TotalDurationSeconds = target.TotalDurationSeconds,

            MusicBrainzID = Text(nameof(ArtistEntity.MusicBrainzID), target.MusicBrainzID, source.MusicBrainzID, conflicts),
            WikipediaUrl = Text(nameof(ArtistEntity.WikipediaUrl), target.WikipediaUrl, source.WikipediaUrl, conflicts),
            OfficialSiteUrl = Text(nameof(ArtistEntity.OfficialSiteUrl), target.OfficialSiteUrl, source.OfficialSiteUrl, conflicts),
            FacebookUrl = Text(nameof(ArtistEntity.FacebookUrl), target.FacebookUrl, source.FacebookUrl, conflicts),
            TwitterUrl = Text(nameof(ArtistEntity.TwitterUrl), target.TwitterUrl, source.TwitterUrl, conflicts),
            FlickrUrl = Text(nameof(ArtistEntity.FlickrUrl), target.FlickrUrl, source.FlickrUrl, conflicts),
            InstagramUrl = Text(nameof(ArtistEntity.InstagramUrl), target.InstagramUrl, source.InstagramUrl, conflicts),
            TiktokUrl = Text(nameof(ArtistEntity.TiktokUrl), target.TiktokUrl, source.TiktokUrl, conflicts),
            ThreadsUrl = Text(nameof(ArtistEntity.ThreadsUrl), target.ThreadsUrl, source.ThreadsUrl, conflicts),
            SongkickUrl = Text(nameof(ArtistEntity.SongkickUrl), target.SongkickUrl, source.SongkickUrl, conflicts),
            SoundcloundUrl = Text(nameof(ArtistEntity.SoundcloundUrl), target.SoundcloundUrl, source.SoundcloundUrl, conflicts),
            ImdbUrl = Text(nameof(ArtistEntity.ImdbUrl), target.ImdbUrl, source.ImdbUrl, conflicts),
            LastFmUrl = Text(nameof(ArtistEntity.LastFmUrl), target.LastFmUrl, source.LastFmUrl, conflicts),
            DiscogsUrl = Text(nameof(ArtistEntity.DiscogsUrl), target.DiscogsUrl, source.DiscogsUrl, conflicts),
            BandsintownUrl = Text(nameof(ArtistEntity.BandsintownUrl), target.BandsintownUrl, source.BandsintownUrl, conflicts),
            YoutubeUrl = Text(nameof(ArtistEntity.YoutubeUrl), target.YoutubeUrl, source.YoutubeUrl, conflicts),
            AllMusicUrl = Text(nameof(ArtistEntity.AllMusicUrl), target.AllMusicUrl, source.AllMusicUrl, conflicts),
            AudioDbID = Text(nameof(ArtistEntity.AudioDbID), target.AudioDbID, source.AudioDbID, conflicts),
            Members = Text(nameof(ArtistEntity.Members), target.Members, source.Members, conflicts),
            SimilarArtists = Text(nameof(ArtistEntity.SimilarArtists), target.SimilarArtists, source.SimilarArtists, conflicts),
            Biography = Text(nameof(ArtistEntity.Biography), target.Biography, source.Biography, conflicts),

            FormedYear = Year(nameof(ArtistEntity.FormedYear), target.FormedYear, source.FormedYear, conflicts),
            BornYear = Year(nameof(ArtistEntity.BornYear), target.BornYear, source.BornYear, conflicts),
            DiedYear = Year(nameof(ArtistEntity.DiedYear), target.DiedYear, source.DiedYear, conflicts),

            GenreId = Reference(nameof(ArtistEntity.GenreId), target.GenreId, source.GenreId, conflicts),
            CountryId = Reference(nameof(ArtistEntity.CountryId), target.CountryId, source.CountryId, conflicts),

            IsFavorite = target.IsFavorite || source.IsFavorite,
            IsLock = target.IsLock || source.IsLock,
            ListenCount = target.ListenCount + source.ListenCount,
            LastListen = MaxDate(target.LastListen, source.LastListen),
            Disbanded = target.Disbanded,
            PictureDominantColor = targetHasPicture ? target.PictureDominantColor : source.PictureDominantColor ?? target.PictureDominantColor,
        };

        if (target.Disbanded != source.Disbanded)
            conflicts.Add(new ArtistMergeFieldConflict(nameof(ArtistEntity.Disbanded), target.Disbanded.ToString(), source.Disbanded.ToString()));

        return new ArtistMergeResult(merged, conflicts);
    }

    private static string? Text(string field, string? target, string? source, List<ArtistMergeFieldConflict> conflicts)
    {
        if (string.IsNullOrWhiteSpace(source))
            return target;

        if (string.IsNullOrWhiteSpace(target))
            return source;

        if (!string.Equals(target.Trim(), source.Trim(), StringComparison.Ordinal))
            conflicts.Add(new ArtistMergeFieldConflict(field, target, source));

        return target;
    }

    private static int? Year(string field, int? target, int? source, List<ArtistMergeFieldConflict> conflicts)
    {
        bool targetEmpty = target is null or 0;
        bool sourceEmpty = source is null or 0;

        if (sourceEmpty)
            return target;

        if (targetEmpty)
            return source;

        if (target != source)
            conflicts.Add(new ArtistMergeFieldConflict(field, target.ToString(), source.ToString()));

        return target;
    }

    private static long? Reference(string field, long? target, long? source, List<ArtistMergeFieldConflict> conflicts)
    {
        bool targetEmpty = target is null or 0;
        bool sourceEmpty = source is null or 0;

        if (sourceEmpty)
            return target;

        if (targetEmpty)
            return source;

        if (target != source)
            conflicts.Add(new ArtistMergeFieldConflict(field, target.ToString(), source.ToString()));

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