using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tags.Query;

public class GetAllTagsQuery : IQuery<IEnumerable<TagDto>>
{
}


public class GetAllTagsQueryHandler(ITagRepository repository) : IQueryHandler<GetAllTagsQuery, IEnumerable<TagDto>>
{
    public async Task<IEnumerable<TagDto>> HandleAsync(GetAllTagsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<TagEntity> tracks = await repository.GetAllAsync();

        return tracks.Select(t => new TagDto() { Id = t.Id, Name = t.Name });
    }
}
