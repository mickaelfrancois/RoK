using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class CreateArtistCommand : ICommand<Result<long>>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public int? GenreId { get; set; }

    public int? CountryId { get; set; }
}

public class CreateArtistCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<CreateArtistCommand, Result<long>>
{
    public async Task<Result<long>> HandleAsync(CreateArtistCommand message, CancellationToken cancellationToken)
    {
        ArtistEntity artistEntity = ArtistDtoMapping.Map(message);

        long id = await _artistRepository.AddAsync(artistEntity);

        if (id > 0)
            return Result<long>.Success(id);
        else
            return Result<long>.Fail("Failed to create artist.");
    }
}
