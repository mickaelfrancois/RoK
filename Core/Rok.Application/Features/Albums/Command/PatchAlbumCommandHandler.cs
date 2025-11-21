using Rok.Application.Interfaces;
using Rok.Shared;

namespace Rok.Application.Features.Albums.Command;

public class PatchAlbumCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public PatchField<string>? Sales { get; set; }

    public PatchField<string>? Label { get; set; }

    public PatchField<string>? Mood { get; set; }

    public PatchField<string>? MusicBrainzID { get; set; }

    public PatchField<string>? Speed { get; set; }

    public PatchField<DateTime?>? ReleaseDate { get; set; }

    public PatchField<string>? ReleaseFormat { get; set; }

    public PatchField<string>? Wikipedia { get; set; }

    public PatchField<string>? Theme { get; set; }

    public PatchField<bool>? IsLive { get; set; }

    public PatchField<bool>? IsBestOf { get; set; }

    public PatchField<bool>? IsCompilation { get; set; }
}


public class PatchAlbumCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<PatchAlbumCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(PatchAlbumCommand message, CancellationToken cancellationToken)
    {
        UpdateAlbumEntity albumEntity = AlbumDtoMapping.Map(message);

        bool result = await _albumRepository.PatchAsync(albumEntity);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to patch album.");
    }
}
