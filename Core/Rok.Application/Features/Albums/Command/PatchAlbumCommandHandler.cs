using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class PatchAlbumCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public string? Sales { get; set; }

    public string? Label { get; set; }

    public string? Mood { get; set; }

    public string? MusicBrainzID { get; set; }

    public string? Speed { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseFormat { get; set; }

    public string? Wikipedia { get; set; }

    public string? Theme { get; set; }
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
