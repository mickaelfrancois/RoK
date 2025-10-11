namespace Rok.Infrastructure.Migration;

public class MigrationService(IDbConnection database) : IMigrationService
{
    private readonly IDbConnection _database = Guard.Against.Null(database);

    public void Initial()
    {
        CreateTableGenres();
        CreateTableArtists();
        CreateTableAlbums();
        CreateTableTracks();
        CreateTableCountries();
        CreateTablePlaylists();
        CreateTablePlaylistTracks();

        SetDatabaseVersion(1);
    }


    public int GetDatabaseVersion()
    {
        return _database.ExecuteScalar<int>("PRAGMA user_version");
    }

    private void SetDatabaseVersion(int version)
    {
        _database.Execute($"PRAGMA user_version={version}");
    }

    private void CreateTableGenres()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Genres")
            .WithIdColumn("id")
            .WithColumn("name").OfType(SqlBuilder.EColumnType.Text).AsNotNull().AsUniqueKey()
            .WithColumn("totalDurationSeconds").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("trackCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("artistCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("compilationCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("bestofCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("albumCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("liveCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("listenCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("isFavorite").OfType(SqlBuilder.EColumnType.Bool).AsNotNull().AsKey()
            .WithColumn("lastListen").OfType(SqlBuilder.EColumnType.DateTime).AsNull().AsKey()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull().AsKey()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTableArtists()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Artists")
            .WithIdColumn("id")
            .WithColumn("name").OfType(SqlBuilder.EColumnType.Text).AsNotNull().AsUniqueKey()
            .WithColumn("wikipediaUrl").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("officialSiteUrl").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("facebookUrl").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("twitterUrl").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("novaUid").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("musicBrainzID").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("yearMini").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("yearMaxi").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("trackCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("albumCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("liveCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("compilationCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("bestofCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("totalDurationSeconds").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("formedYear").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("bornYear").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("diedYear").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("disbanded").OfType(SqlBuilder.EColumnType.Bool).AsNotNull("0")
            .WithColumn("style").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("gender").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("mood").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("members").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("similarArtists").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("biography").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("countryId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("genreId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("isFavorite").OfType(SqlBuilder.EColumnType.Bool).AsNotNull("0").AsKey()
            .WithColumn("listenCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull("0").AsKey()
            .WithColumn("lastListen").OfType(SqlBuilder.EColumnType.DateTime).AsNull().AsKey()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull().AsKey()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTableAlbums()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Albums")
            .WithIdColumn("id")
            .WithColumn("name").OfType(SqlBuilder.EColumnType.Text).AsNotNull().AsKey()
            .WithColumn("year").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("isLive").OfType(SqlBuilder.EColumnType.Bool).AsNotNull().AsKey()
            .WithColumn("isCompilation").OfType(SqlBuilder.EColumnType.Bool).AsNotNull().AsKey()
            .WithColumn("isBestof").OfType(SqlBuilder.EColumnType.Bool).AsNotNull().AsKey()
            .WithColumn("wikipedia").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("novaUid").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("trackCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("duration").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("releaseDate").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("label").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("speed").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("theme").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("mood").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("sales").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("releaseFormat").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("musicBrainzId").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("albumPath").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("artistId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("genreId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("isFavorite").OfType(SqlBuilder.EColumnType.Bool).AsNotNull().AsKey()
            .WithColumn("listenCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("lastListen").OfType(SqlBuilder.EColumnType.DateTime).AsNull().AsKey()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull().AsKey()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTableTracks()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Tracks")
            .WithIdColumn("id")
            .WithColumn("title").OfType(SqlBuilder.EColumnType.Text).AsNotNull().AsKey()
            .WithColumn("artistId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("albumId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("genreId").OfType(SqlBuilder.EColumnType.Integer).AsNull().AsKey()
            .WithColumn("trackNumber").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("duration").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("size").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("bitrate").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("novauid").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("musicBrainzId").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("musicFile").OfType(SqlBuilder.EColumnType.Text).AsNotNull().AsUniqueKey()
            .WithColumn("fileDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull()
            .WithColumn("isLive").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("score").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("listenCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("lastListen").OfType(SqlBuilder.EColumnType.Text).AsNull().AsKey()
            .WithColumn("skipCount").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("lastSkip").OfType(SqlBuilder.EColumnType.DateTime).AsNull().AsKey()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull().AsKey()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTableCountries()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Countries")
            .WithIdColumn("id")
            .WithColumn("code").OfType(SqlBuilder.EColumnType.Text).AsNotNull()
            .WithColumn("french").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("english").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTablePlaylists()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("Playlists")
            .WithIdColumn("id")
            .WithColumn("name").OfType(SqlBuilder.EColumnType.Text).AsNotNull()
            .WithColumn("picture").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("trackMaximum").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("durationMaximum").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("trackCount").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("duration").OfType(SqlBuilder.EColumnType.Integer).AsNull()
            .WithColumn("groupsJson").OfType(SqlBuilder.EColumnType.Text).AsNull()
            .WithColumn("type").OfType(SqlBuilder.EColumnType.Integer).AsNotNull()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }

    private void CreateTablePlaylistTracks()
    {
        SqlBuilder builder = new();

        string table = builder.CreateTable("PlaylistTracks")
            .WithIdColumn("id")
            .WithColumn("playlistId").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("trackId").OfType(SqlBuilder.EColumnType.Integer).AsNotNull().AsKey()
            .WithColumn("position").OfType(SqlBuilder.EColumnType.Integer).AsNotNull()
            .WithColumn("listened").OfType(SqlBuilder.EColumnType.Integer).AsNotNull()
            .WithColumn("creatDate").OfType(SqlBuilder.EColumnType.DateTime).AsNotNull()
            .WithColumn("editDate").OfType(SqlBuilder.EColumnType.DateTime).AsNull()
            .ToSql();

        CreateTable(table);
    }


    private void CreateTable(string sql)
    {
        using IDbCommand command = _database.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}