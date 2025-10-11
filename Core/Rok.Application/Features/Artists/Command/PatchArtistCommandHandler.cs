using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Artists.Command;

public class PatchArtistCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public string? WikipediaUrl { get; set; }

    public string? OfficialSiteUrl { get; set; }

    public string? FacebookUrl { get; set; }

    public string? TwitterUrl { get; set; }

    public string? NovaUid { get; set; }

    public string? MusicBrainzID { get; set; }

    public int? FormedYear { get; set; }

    public int? BornYear { get; set; }

    public int? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public string? Style { get; set; }

    public string? Gender { get; set; }

    public string? Mood { get; set; }

    public string? Biography { get; set; }
}

public class PatchArtistCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<PatchArtistCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(PatchArtistCommand message, CancellationToken cancellationToken)
    {
        IUpdateArtistEntity artistEntity = ArtistDtoMapping.Map(message);

        bool result = await _artistRepository.PatchAsync(artistEntity);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to patch artist.");
    }
}
