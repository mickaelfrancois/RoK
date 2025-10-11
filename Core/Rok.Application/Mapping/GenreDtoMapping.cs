namespace Rok.Application.Mapping;

internal static class GenreDtoMapping
{
    public static GenreDto Map(GenreEntity genre)
    {
        return new GenreDto
        {
            Name = genre.Name,
            ArtistCount = genre.ArtistCount,
            TrackCount = genre.TrackCount,
            AlbumCount = genre.AlbumCount,
            LiveCount = genre.LiveCount,
            CompilationCount = genre.CompilationCount,
            BestofCount = genre.BestofCount,
            IsFavorite = genre.IsFavorite,
            ListenCount = genre.ListenCount,
            LastListen = genre.LastListen,
            Id = genre.Id,
            CreatDate = genre.CreatDate,
            EditDate = genre.EditDate
        };
    }
}
