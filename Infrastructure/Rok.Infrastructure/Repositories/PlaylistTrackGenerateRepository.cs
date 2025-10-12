using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Interfaces;
using Rok.Shared.Enums;
using Rok.Shared.Extensions;
using System.Text;

namespace Rok.Infrastructure.Repositories;

public class PlaylistTrackGenerateRepository(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, ILogger<PlaylistTrackGenerateRepository> logger) : GenericRepository<TrackEntity>(db, backgroundDb, null, logger), IPlaylistTrackGenerateRepository
{
    public async Task<List<TrackEntity>> GenerateAsync(GeneratePlaylistTracksQuery request, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        StringBuilder query = new();
        Dictionary<string, object> parameters = [];

        query.Append(GetSelectQuery() + " WHERE 1=1 ");

        PlaylistGroupDto group = request.Group;

        HandleNameFilter(group.Filters.Where(c => c.Entity == SmartPlaylistEntity.Genres && c.Field == SmartPlaylistField.Name && !string.IsNullOrEmpty(c.Value)), query, parameters);
        HandleNameFilter(group.Filters.Where(c => c.Entity == SmartPlaylistEntity.Artists && c.Field == SmartPlaylistField.Name && !string.IsNullOrEmpty(c.Value)), query, parameters);
        HandleNameFilter(group.Filters.Where(c => c.Entity == SmartPlaylistEntity.Albums && c.Field == SmartPlaylistField.Name && !string.IsNullOrEmpty(c.Value)), query, parameters);
        HandleNameFilter(group.Filters.Where(c => c.Entity == SmartPlaylistEntity.Countries && c.Field == SmartPlaylistField.Code && !string.IsNullOrEmpty(c.Value)), query, parameters);

        HandleIntFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Int && c.Operator != SmartPlaylistOperator.Between), query, parameters);
        HandleBoolFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Bool && c.Operator != SmartPlaylistOperator.Between), query, parameters);
        HandleDateFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Date && c.Operator != SmartPlaylistOperator.Between), query, parameters);

        HandleDayFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Day && c.Operator != SmartPlaylistOperator.Between), query, parameters);

        HandleBetweenIntFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Int && c.Operator == SmartPlaylistOperator.Between), query, parameters);
        HandleBetweenDateFilter(group.Filters.Where(c => c.FieldType == SmartPlaylistFieldType.Date && c.Operator == SmartPlaylistOperator.Between), query, parameters);

        HandleSort(group.SortBy, query);

        query.Append($" LIMIT {request.PlaylistTrackCount} ");

        _logger.LogTrace("Query: {Query}", query.ToString());

        List<TrackEntity> rows = (await ExecuteQueryAsync(query.ToString(), kind, parameters)).ToList();

        if (rows.Count > 0)
            rows.Shuffle();

        return rows;
    }

    private static void HandleNameFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        string groupOp = "";

        if (!filters.Any())
            return;

        query.Append(" AND (");

        foreach (PlaylistFilterDto filter in filters)
        {
            string op = "";
            string value = filter.Value!;

            switch (filter.Operator)
            {
                case SmartPlaylistOperator.Equals:
                    op = "=";
                    break;
                case SmartPlaylistOperator.NotEquals:
                    op = "!=";
                    break;
                case SmartPlaylistOperator.Contains:
                    op = "LIKE";
                    value = $"%{value}%";
                    break;
                case SmartPlaylistOperator.NotContains:
                    op = "NOT LIKE";
                    value = $"%{value}%";
                    break;
                case SmartPlaylistOperator.StartsWith:
                    op = "LIKE";
                    value = $"{value}%";
                    break;
                case SmartPlaylistOperator.EndsWith:
                    op = "LIKE";
                    value = $"%{value}";
                    break;
            }

            string parameter = $"param{parameters.Count}";
            query.Append($" {groupOp} ({filter.Entity}.{filter.Field} {op} @{parameter}) ");
            parameters.Add($"{parameter}", value);
            groupOp = " OR ";
        }

        query.Append(" ) ");
    }

    private static void HandleIntFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            int value = Convert.ToInt32(filter.Value);
            string parameter;
            string op = "";

            switch (filter.Operator)
            {
                case SmartPlaylistOperator.GreaterThan:
                    op = ">";
                    break;
                case SmartPlaylistOperator.LessThan:
                    op = "<";
                    break;
                case SmartPlaylistOperator.Equals:
                    op = "=";
                    break;
                case SmartPlaylistOperator.NotEquals:
                    op = "!=";
                    break;
            }

            parameter = $"param{parameters.Count}";
            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter} ");
            parameters.Add($"{parameter}", value);
        }
    }

    private static void HandleBetweenIntFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            int value = Convert.ToInt32(filter.Value);
            int value2 = Convert.ToInt32(filter.Value2!);

            string parameter1;
            string parameter2;
            string op = "BETWEEN";

            parameter1 = $"param{parameters.Count}";
            parameters.Add($"{parameter1}", value);

            parameter2 = $"param{parameters.Count}";
            parameters.Add($"{parameter2}", value2);

            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter1} AND @{parameter2} ");
        }
    }

    private static void HandleBoolFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            bool value = Convert.ToBoolean(filter.Value);
            string parameter;
            string op = "";

            switch (filter.Operator)
            {
                case SmartPlaylistOperator.Equals:
                    op = "=";
                    break;
                case SmartPlaylistOperator.NotEquals:
                    op = "!=";
                    break;
            }

            parameter = $"param{parameters.Count}";
            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter} ");
            parameters.Add($"{parameter}", value);
        }
    }

    private static void HandleDateFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            DateOnly value = DateOnly.ParseExact(filter.Value!, "yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);

            string parameter;
            string op = "";

            switch (filter.Operator)
            {
                case SmartPlaylistOperator.Equals:
                    op = "=";
                    break;
                case SmartPlaylistOperator.NotEquals:
                    op = "!=";
                    break;
                case SmartPlaylistOperator.LessThan:
                    op = "<";
                    break;
                case SmartPlaylistOperator.GreaterThan:
                    op = ">";
                    break;
            }

            parameter = $"param{parameters.Count}";
            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter} ");
            parameters.Add($"{parameter}", value);
        }
    }

    private static void HandleBetweenDateFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            DateOnly value = DateOnly.ParseExact(filter.Value!, "yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
            DateOnly value2 = DateOnly.ParseExact(filter.Value2!, "yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);

            string parameter1;
            string parameter2;
            string op = "BETWEEN";

            parameter1 = $"param{parameters.Count}";
            parameters.Add($"{parameter1}", value);

            parameter2 = $"param{parameters.Count}";
            parameters.Add($"{parameter2}", value2);

            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter1} AND @{parameter2} ");
        }
    }

    private static void HandleDayFilter(IEnumerable<PlaylistFilterDto> filters, StringBuilder query, Dictionary<string, object> parameters)
    {
        if (!filters.Any())
            return;

        foreach (PlaylistFilterDto filter in filters)
        {
            int value = Convert.ToInt32(filter.Value);
            DateTime refDate = DateTime.Now.AddDays(-value);
            string parameter;
            string op = "";

            switch (filter.Operator)
            {
                case SmartPlaylistOperator.Equals:
                    op = "=";
                    break;
                case SmartPlaylistOperator.NotEquals:
                    op = "!=";
                    break;
                case SmartPlaylistOperator.LessThan:
                    op = ">";
                    break;
                case SmartPlaylistOperator.GreaterThan:
                    op = "<";
                    break;
            }

            parameter = $"param{parameters.Count}";
            query.Append($" AND {filter.Entity}.{filter.Field} {op} @{parameter} ");
            parameters.Add($"{parameter}", refDate);
        }
    }

    private static void HandleSort(SmartPlaylistSelectBy sort, StringBuilder query)
    {
        switch (sort)
        {
            case SmartPlaylistSelectBy.Random:
                query.Append(" ORDER BY RANDOM() ");
                break;
            case SmartPlaylistSelectBy.Newest:
                query.Append(" ORDER BY tracks.creatdate DESC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.Oldest:
                query.Append(" ORDER BY tracks.creatdate ASC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.MostPlayed:
                query.Append(" ORDER BY tracks.listencount DESC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.LeastPlayed:
                query.Append(" ORDER BY tracks.listencount ASC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.HighestRated:
                query.Append(" ORDER BY tracks.score DESC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.LowestRated:
                query.Append(" ORDER BY tracks.score ASC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.MostRecent:
                query.Append(" ORDER BY tracks.lastlisten DESC, RANDOM() ");
                break;
            case SmartPlaylistSelectBy.LeastRecent:
                query.Append(" ORDER BY tracks.lastlisten ASC, RANDOM() ");
                break;
            default:
                query.Append(" ORDER BY RANDOM() ");
                break;
        }
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT tracks.*,
                     albums.name AS albumName, albums.isFavorite AS isAlbumFavorite, albums.isCompilation AS isAlbumCompilation, albums.isLive AS isAlbumLive,
                     artists.name AS artistName, artists.isFavorite AS isArtistFavorite, 
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite, 
                     countries.code AS countryCode, countries.english AS countryName
                     FROM tracks 
                     LEFT JOIN artists ON artists.Id = tracks.artistId 
                     LEFT JOIN albums ON albums.Id = tracks.albumId 
                     LEFT JOIN genres ON genres.Id = tracks.genreId 
                     LEFT JOIN countries ON countries.Id = artists.countryId 
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE tracks.{whereParam} = @{whereParam}";

        return query;
    }
}
