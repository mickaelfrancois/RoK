namespace Rok.Application.Interfaces;

public interface ITagRepository : IRepository<TagEntity>
{
    Task<bool> UpdateEntityTagsAsync(long entityId, IEnumerable<string> tags, string linkTableName, string linkColumnName);
}
