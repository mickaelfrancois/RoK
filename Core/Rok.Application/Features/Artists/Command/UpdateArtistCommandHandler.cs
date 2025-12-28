using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class UpdateArtistCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public string? MusicBrainzID { get; set; }

    /* Social networks and external links */

    public string? WikipediaUrl { get; set; }

    public string? OfficialSiteUrl { get; set; }

    public string? FacebookUrl { get; set; }

    public string? TwitterUrl { get; set; }

    public string? FlickrUrl { get; set; }

    public string? InstagramUrl { get; set; }

    public string? TiktokUrl { get; set; }

    public string? ThreadsUrl { get; set; }

    public string? SongkickUrl { get; set; }

    public string? SoundcloundUrl { get; set; }

    public string? ImdbUrl { get; set; }

    public string? LastFmUrl { get; set; }

    public string? DiscogsUrl { get; set; }

    public string? BandsintownUrl { get; set; }

    public string? YoutubeUrl { get; set; }

    public string? AudioDbID { get; set; }

    public string? AllMusicUrl { get; set; }

    public int? FormedYear { get; set; }

    public int? BornYear { get; set; }

    public int? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public string? Members { get; set; }

    public string? SimilarArtists { get; set; }

    public string? Biography { get; set; }
}


public class UpdateArtistCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<UpdateArtistCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistCommand command, CancellationToken cancellationToken)
    {
        ArtistEntity? entity = await _artistRepository.GetByIdAsync(command.Id);
        if (entity is null)
            return Result<bool>.Fail("Artist not found.");

        entity.YoutubeUrl = command.YoutubeUrl;
        entity.MusicBrainzID = command.MusicBrainzID;
        entity.WikipediaUrl = command.WikipediaUrl;
        entity.OfficialSiteUrl = command.OfficialSiteUrl;
        entity.FacebookUrl = command.FacebookUrl;
        entity.TwitterUrl = command.TwitterUrl;
        entity.FlickrUrl = command.FlickrUrl;
        entity.InstagramUrl = command.InstagramUrl;
        entity.TiktokUrl = command.TiktokUrl;
        entity.ThreadsUrl = command.ThreadsUrl;
        entity.SongkickUrl = command.SongkickUrl;
        entity.SoundcloundUrl = command.SoundcloundUrl;
        entity.ImdbUrl = command.ImdbUrl;
        entity.LastFmUrl = command.LastFmUrl;
        entity.DiscogsUrl = command.DiscogsUrl;
        entity.BandsintownUrl = command.BandsintownUrl;
        entity.AudioDbID = command.AudioDbID;
        entity.AllMusicUrl = command.AllMusicUrl;
        entity.FormedYear = command.FormedYear;
        entity.BornYear = command.BornYear;
        entity.DiedYear = command.DiedYear;
        entity.Disbanded = command.Disbanded;
        entity.Members = command.Members;
        entity.SimilarArtists = command.SimilarArtists;

        if (!string.IsNullOrWhiteSpace(command.Biography))
            entity.Biography = command.Biography;

        bool result = await _artistRepository.UpdateAsync(entity);

        if (result)
            return Result<bool>.Success(true);
        else
            return Result<bool>.Fail("Failed to update artist.");
    }
}
