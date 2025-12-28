using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class UpdateAlbumCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public string? MusicBrainzID { get; set; }

    public string? ReleaseGroupMusicBrainzID { get; set; }

    public string? Sales { get; set; }

    public string? AudioDbID { get; set; }

    public string? AudioDbArtistID { get; set; }

    public string? AllMusicID { get; set; }

    public string? DiscogsID { get; set; }

    public string? MusicMozID { get; set; }

    public string? LyricWikiID { get; set; }

    public string? GeniusID { get; set; }

    public string? WikipediaID { get; set; }

    public string? WikidataID { get; set; }

    public string? AmazonID { get; set; }

    public string? Label { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? Wikipedia { get; set; }

    public bool? IsLive { get; set; }

    public bool? IsBestOf { get; set; }

    public bool? IsCompilation { get; set; }

    public string? Biography { get; set; }
}


public class UpdateAlbumCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumCommand command, CancellationToken cancellationToken)
    {
        AlbumEntity? entity = await _albumRepository.GetByIdAsync(command.Id);
        if (entity is null)
            return Result<bool>.Fail("Album not found.");

        entity.MusicBrainzID = command.MusicBrainzID;
        entity.ReleaseGroupMusicBrainzID = command.ReleaseGroupMusicBrainzID;
        entity.Sales = command.Sales;
        entity.AudioDbID = command.AudioDbID;
        entity.AudioDbArtistID = command.AudioDbArtistID;
        entity.AllMusicID = command.AllMusicID;
        entity.DiscogsID = command.DiscogsID;
        entity.MusicMozID = command.MusicMozID;
        entity.LyricWikiID = command.LyricWikiID;
        entity.GeniusID = command.GeniusID;
        entity.WikipediaID = command.WikipediaID;
        entity.WikidataID = command.WikidataID;
        entity.AmazonID = command.AmazonID;
        entity.Label = command.Label;
        entity.ReleaseDate = command.ReleaseDate;
        entity.Wikipedia = command.Wikipedia;

        if (command.IsLive.HasValue)
            entity.IsLive = command.IsLive.Value;
        if (command.IsBestOf.HasValue)
            entity.IsBestOf = command.IsBestOf.Value;
        if (command.IsCompilation.HasValue)
            entity.IsCompilation = command.IsCompilation.Value;
        if (command.IsLive.HasValue)
            entity.IsLive = command.IsLive.Value;

        if (!string.IsNullOrWhiteSpace(command.Biography))
            entity.Biography = command.Biography;

        bool result = await _albumRepository.UpdateAsync(entity);

        if (result)
            return Result<bool>.Success(true);
        else
            return Result<bool>.Fail("Failed to update album.");
    }
}
