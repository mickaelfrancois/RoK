using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Requests;


public class UpdateArtistTagsRequest(long id, IEnumerable<string> tags) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;


    public IEnumerable<string> Tags { get; set; } = tags;
}

public sealed class UpdateArtistTagsRequestValidator : Validator<UpdateArtistTagsRequest>
{
    public UpdateArtistTagsRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}


public class UpdateArtistTagsRequestHandler(ITagRepository repository) : IRequestHandler<UpdateArtistTagsRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistTagsRequest message, CancellationToken cancellationToken)
    {
        bool result = await repository.UpdateEntityTagsAsync(message.Id, message.Tags, "artisttags", "artistid");

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist tags.");
    }
}
