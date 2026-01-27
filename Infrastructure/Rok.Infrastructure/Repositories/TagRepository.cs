using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiF.SimpleMessenger;
using Rok.Application.Interfaces;
using Rok.Application.Messages;

namespace Rok.Infrastructure.Repositories;

public class TagRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<TagRepository> logger) : GenericRepository<TagEntity>(connection, backgroundConnection, null, logger), ITagRepository
{
    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = "SELECT tags.* FROM tags ";

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE tags.{whereParam} = @{whereParam}";

        return query;
    }


    public async Task<bool> UpdateEntityTagsAsync(long entityId, IEnumerable<string> tags, string linkTableName, string linkColumnName)
    {
        using IDbTransaction transaction = _connection.BeginTransaction();

        try
        {
            string deleteSql = $"DELETE FROM {linkTableName} WHERE {linkColumnName} = @entityId";
            await ExecuteNonQueryAsync(deleteSql, transaction, new { entityId });

            List<string> cleanTags = tags?.Select(t => t.Trim())
                                         .Where(t => !string.IsNullOrEmpty(t))
                                         .Distinct()
                                         .ToList() ?? new List<string>();

            if (cleanTags.Count > 0)
            {
                await ExecuteNonQueryAsync("INSERT OR IGNORE INTO tags (name) VALUES (@tagName)", transaction, cleanTags.Select(t => new { tagName = t }));

                IEnumerable<long> tagIds = await _connection.QueryAsync<long>("SELECT id FROM tags WHERE name IN @cleanTags", new { cleanTags }, transaction);

                var linkParameters = tagIds.Select(id => new { entityId, tagId = id }).ToList();
                string insertLinkSql = $"INSERT INTO {linkTableName} ({linkColumnName}, tagId) VALUES (@entityId, @tagId)";

                await ExecuteNonQueryAsync(insertLinkSql, transaction, linkParameters);
            }

            transaction.Commit();

            Messenger.Send(new TagUpdatedMessage());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tags for {Table} ID {Id}", linkTableName, entityId);
            transaction.Rollback();
            return false;
        }
    }


    public override string GetTableName()
    {
        return "tags";
    }
}
