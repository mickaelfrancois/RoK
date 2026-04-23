using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;


public class UpdateAlbumTagsCommand(long id, IEnumerable<string> tags) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;


    public IEnumerable<string> Tags { get; set; } = tags;
}


public class UpdateAlbumTagsCommandHandler(ITagRepository repository) : ICommandHandler<UpdateAlbumTagsCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumTagsCommand message, CancellationToken cancellationToken)
    {
        bool result = await repository.UpdateEntityTagsAsync(message.Id, message.Tags, "albumtags", "albumid");

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album tags.");
    }
}