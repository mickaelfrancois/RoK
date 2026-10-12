using Rok.Application.Interfaces.Repositories;
using Rok.Shared;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Artists.Requests;

public class UpdateArtistRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }

    public PatchField<string?> MusicBrainzID { get; set; } = new();

    /* Social networks and external links */

    public PatchField<string?> WikipediaUrl { get; set; } = new();

    public PatchField<string?> OfficialSiteUrl { get; set; } = new();

    public PatchField<string?> FacebookUrl { get; set; } = new();

    public PatchField<string?> TwitterUrl { get; set; } = new();

    public PatchField<string?> FlickrUrl { get; set; } = new();

    public PatchField<string?> InstagramUrl { get; set; } = new();

    public PatchField<string?> TiktokUrl { get; set; } = new();

    public PatchField<string?> ThreadsUrl { get; set; } = new();

    public PatchField<string?> SongkickUrl { get; set; } = new();

    public PatchField<string?> SoundcloundUrl { get; set; } = new();

    public PatchField<string?> ImdbUrl { get; set; } = new();

    public PatchField<string?> LastFmUrl { get; set; } = new();

    public PatchField<string?> DiscogsUrl { get; set; } = new();

    public PatchField<string?> BandsintownUrl { get; set; } = new();

    public PatchField<string?> YoutubeUrl { get; set; } = new();

    public PatchField<string?> AudioDbID { get; set; } = new();

    public PatchField<string?> AllMusicUrl { get; set; } = new();

    public PatchField<int?> FormedYear { get; set; } = new();

    public PatchField<int?> BornYear { get; set; } = new();

    public PatchField<int?> DiedYear { get; set; } = new();

    public PatchField<bool> Disbanded { get; set; } = new();

    public PatchField<string?> Members { get; set; } = new();

    public PatchField<string?> SimilarArtists { get; set; } = new();

    public PatchField<string?> Biography { get; set; } = new();

    public PatchField<bool> IsLock { get; set; } = new();
}

public sealed class UpdateArtistRequestValidator : Validator<UpdateArtistRequest>
{
    public UpdateArtistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}


public class UpdateArtistRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<UpdateArtistRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistRequest command, CancellationToken cancellationToken)
    {
        ArtistEntity? entity = await _artistRepository.GetByIdAsync(command.Id);
        if (entity is null)
            return Result<bool>.Fail(NotFoundError.ForEntity("Artist", command.Id));

        bool identifierChanged = false;

        if (command.MusicBrainzID.TryGetValue(out string? musicBrainzID))
        {
            identifierChanged = entity.MusicBrainzID.AreDifferent(musicBrainzID);
            entity.MusicBrainzID = musicBrainzID;
        }

        if (command.WikipediaUrl.TryGetValue(out string? wikipediaUrl))
            entity.WikipediaUrl = wikipediaUrl;

        if (command.OfficialSiteUrl.TryGetValue(out string? officialSiteUrl))
            entity.OfficialSiteUrl = officialSiteUrl;

        if (command.FacebookUrl.TryGetValue(out string? facebookUrl))
            entity.FacebookUrl = facebookUrl;

        if (command.TwitterUrl.TryGetValue(out string? twitterUrl))
            entity.TwitterUrl = twitterUrl;

        if (command.FlickrUrl.TryGetValue(out string? flickrUrl))
            entity.FlickrUrl = flickrUrl;

        if (command.InstagramUrl.TryGetValue(out string? instagramUrl))
            entity.InstagramUrl = instagramUrl;

        if (command.TiktokUrl.TryGetValue(out string? tiktokUrl))
            entity.TiktokUrl = tiktokUrl;

        if (command.ThreadsUrl.TryGetValue(out string? threadsUrl))
            entity.ThreadsUrl = threadsUrl;

        if (command.SongkickUrl.TryGetValue(out string? songkickUrl))
            entity.SongkickUrl = songkickUrl;

        if (command.SoundcloundUrl.TryGetValue(out string? soundcloundUrl))
            entity.SoundcloundUrl = soundcloundUrl;

        if (command.ImdbUrl.TryGetValue(out string? imdbUrl))
            entity.ImdbUrl = imdbUrl;

        if (command.LastFmUrl.TryGetValue(out string? lastFmUrl))
            entity.LastFmUrl = lastFmUrl;

        if (command.DiscogsUrl.TryGetValue(out string? discogsUrl))
            entity.DiscogsUrl = discogsUrl;

        if (command.BandsintownUrl.TryGetValue(out string? bandsintownUrl))
            entity.BandsintownUrl = bandsintownUrl;

        if (command.YoutubeUrl.TryGetValue(out string? youtubeUrl))
            entity.YoutubeUrl = youtubeUrl;

        if (command.AudioDbID.TryGetValue(out string? audioDbID))
            entity.AudioDbID = audioDbID;

        if (command.AllMusicUrl.TryGetValue(out string? allMusicUrl))
            entity.AllMusicUrl = allMusicUrl;

        if (command.FormedYear.TryGetValue(out int? formedYear))
            entity.FormedYear = formedYear;

        if (command.BornYear.TryGetValue(out int? bornYear))
            entity.BornYear = bornYear;

        if (command.DiedYear.TryGetValue(out int? diedYear))
            entity.DiedYear = diedYear;

        if (command.Disbanded.TryGetValue(out bool disbanded))
            entity.Disbanded = disbanded;

        if (command.Members.TryGetValue(out string? members))
            entity.Members = members;

        if (command.SimilarArtists.TryGetValue(out string? similarArtists))
            entity.SimilarArtists = similarArtists;

        if (command.Biography.TryGetValue(out string? biography))
            entity.Biography = biography;

        if (command.IsLock.TryGetValue(out bool isLock))
            entity.IsLock = isLock;

        if (identifierChanged)
            entity.GetMetaDataLastAttempt = null;

        bool result = await _artistRepository.UpdateAsync(entity);

        if (result)
            return Result<bool>.Ok(true);
        else
            return Result<bool>.Fail(new OperationError("artist.update_failed", "Failed to update artist."));
    }
}