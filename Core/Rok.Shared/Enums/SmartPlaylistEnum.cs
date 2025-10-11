namespace Rok.Shared.Enums;

public enum SmartPlaylistEntity
{
    Tracks,
    Albums,
    Artists,
    Genres,
    Countries
}

public enum SmartPlaylistField
{
    IsFavorite,
    IsLive,
    IsCompilation,
    IsBestof,
    Name,
    Code,
    Year,
    ReleaseDate,
    ListenCount,
    LastListen,
    CreatDate,
    TrackCount,
    AlbumCount,
    ArtistCount,
    LiveCount,
    CompilationCount,
    BestofCount,
    Duration,
    Score,
    SkipCount,
    Size,
    Bitrate
}

public enum SmartPlaylistFieldType
{
    Int,
    Bool,
    String,
    Date,
    Day
}

public enum SmartPlaylistOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Between,
}

public enum SmartPlaylistLimit
{
    Rows,
    Minutes,
    Mo
}

public enum SmartPlaylistSelectBy
{
    Random,
    MostPlayed,
    LeastPlayed,
    MostRecent,
    LeastRecent,
    HighestRated,
    LowestRated,
    Oldest,
    Newest
}