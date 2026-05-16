using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumTagsRequest(long id, IEnumerable<string> tags) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public IEnumerable<string> Tags { get; set; } = tags;
}

public sealed class UpdateAlbumTagsRequestValidator : Validator<UpdateAlbumTagsRequest>
{
    public UpdateAlbumTagsRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}


public class UpdateAlbumTagsRequestHandler(ITagRepository repository) : IRequestHandler<UpdateAlbumTagsRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumTagsRequest message, CancellationToken cancellationToken)
    {
        bool result = await repository.UpdateEntityTagsAsync(message.Id, message.Tags, "albumtags", "albumid");

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.tags_update_failed", "Failed to update album tags."));
    }
}
