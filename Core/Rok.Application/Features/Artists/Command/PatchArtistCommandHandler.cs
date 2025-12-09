using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;
using Rok.Shared;

namespace Rok.Application.Features.Artists.Command;

public class PatchArtistCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public PatchField<string>? WikipediaUrl { get; set; }

    public PatchField<string>? OfficialSiteUrl { get; set; }

    public PatchField<string>? FacebookUrl { get; set; }

    public PatchField<string>? TwitterUrl { get; set; }

    public PatchField<string>? NovaUid { get; set; }

    public PatchField<string>? MusicBrainzID { get; set; }

    public PatchField<int>? FormedYear { get; set; }

    public PatchField<int>? BornYear { get; set; }

    public PatchField<int>? DiedYear { get; set; }

    public PatchField<bool>? Disbanded { get; set; }

    public PatchField<string>? Style { get; set; }

    public PatchField<string>? Gender { get; set; }

    public PatchField<string>? Mood { get; set; }

    public PatchField<string>? Biography { get; set; }
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
