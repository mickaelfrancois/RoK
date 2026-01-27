using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;


public class UpdateArtistTagsCommand(long id, IEnumerable<string> tags) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;


    public IEnumerable<string> Tags { get; set; } = tags;
}


public class UpdateArtistTagsCommandHandler(ITagRepository repository) : ICommandHandler<UpdateArtistTagsCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistTagsCommand message, CancellationToken cancellationToken)
    {
        bool result = await repository.UpdateEntityTagsAsync(message.Id, message.Tags, "artisttags", "artistid");

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist tags.");
    }
}