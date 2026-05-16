using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tags.Requests;

public class GetAllTagsRequest : IRequest<IEnumerable<TagDto>>
{
}


public class GetAllTagsRequestHandler(ITagRepository repository) : IRequestHandler<GetAllTagsRequest, IEnumerable<TagDto>>
{
    public async Task<IEnumerable<TagDto>> Handle(GetAllTagsRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TagEntity> tracks = await repository.GetAllAsync();

        return tracks.Select(t => new TagDto() { Id = t.Id, Name = t.Name });
    }
}