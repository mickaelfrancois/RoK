using Rok.Application.Interfaces;
using Rok.Shared;

namespace Rok.Application.Features.Albums.Command;

public class UpdateAlbumCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }

    public PatchField<string?> MusicBrainzID { get; set; } = new();

    public PatchField<string?> ReleaseGroupMusicBrainzID { get; set; } = new();

    public PatchField<string?> Sales { get; set; } = new();

    public PatchField<string?> AudioDbID { get; set; } = new();

    public PatchField<string?> AudioDbArtistID { get; set; } = new();

    public PatchField<string?> AllMusicID { get; set; } = new();

    public PatchField<string?> DiscogsID { get; set; } = new();

    public PatchField<string?> MusicMozID { get; set; } = new();

    public PatchField<string?> LyricWikiID { get; set; } = new();

    public PatchField<string?> GeniusID { get; set; } = new();

    public PatchField<string?> WikipediaID { get; set; } = new();

    public PatchField<string?> WikidataID { get; set; } = new();

    public PatchField<string?> AmazonID { get; set; } = new();

    public PatchField<string?> Label { get; set; } = new();

    public PatchField<DateTime?> ReleaseDate { get; set; } = new();

    public PatchField<string?> Wikipedia { get; set; } = new();

    public PatchField<bool> IsLive { get; set; } = new();

    public PatchField<bool> IsBestOf { get; set; } = new();

    public PatchField<bool> IsCompilation { get; set; } = new();

    public PatchField<string?> Biography { get; set; } = new();

    public PatchField<bool> IsLock { get; set; } = new();

    public PatchField<string?> LastFmUrl { get; set; } = new();
}


public class UpdateAlbumCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumCommand command, CancellationToken cancellationToken)
    {
        AlbumEntity? entity = await _albumRepository.GetByIdAsync(command.Id);
        if (entity is null)
            return Result<bool>.Fail("Album not found.");

        if (command.MusicBrainzID.TryGetValue(out string? musicBrainzID))
            entity.MusicBrainzID = musicBrainzID;

        if (command.ReleaseGroupMusicBrainzID.TryGetValue(out string? releaseGroupMusicBrainzID))
            entity.ReleaseGroupMusicBrainzID = releaseGroupMusicBrainzID;

        if (command.Sales.TryGetValue(out string? sales))
            entity.Sales = sales;

        if (command.AudioDbID.TryGetValue(out string? audioDbID))
            entity.AudioDbID = audioDbID;

        if (command.AudioDbArtistID.TryGetValue(out string? audioDbArtistID))
            entity.AudioDbArtistID = audioDbArtistID;

        if (command.AllMusicID.TryGetValue(out string? allMusicID))
            entity.AllMusicID = allMusicID;

        if (command.DiscogsID.TryGetValue(out string? discogsID))
            entity.DiscogsID = discogsID;

        if (command.MusicMozID.TryGetValue(out string? musicMozID))
            entity.MusicMozID = musicMozID;

        if (command.LyricWikiID.TryGetValue(out string? lyricWikiID))
            entity.LyricWikiID = lyricWikiID;

        if (command.GeniusID.TryGetValue(out string? geniusID))
            entity.GeniusID = geniusID;

        if (command.WikipediaID.TryGetValue(out string? wikipediaID))
            entity.WikipediaID = wikipediaID;

        if (command.WikidataID.TryGetValue(out string? wikidataID))
            entity.WikidataID = wikidataID;

        if (command.AmazonID.TryGetValue(out string? amazonID))
            entity.AmazonID = amazonID;

        if (command.Label.TryGetValue(out string? label))
            entity.Label = label;

        if (command.Wikipedia.TryGetValue(out string? wikipedia))
            entity.Wikipedia = wikipedia;

        if (command.ReleaseDate.TryGetValue(out DateTime? releaseDate))
            entity.ReleaseDate = releaseDate;

        if (command.IsLive.TryGetValue(out bool isLive))
            entity.IsLive = isLive;

        if (command.IsBestOf.TryGetValue(out bool isBestOf))
            entity.IsBestOf = isBestOf;

        if (command.IsCompilation.TryGetValue(out bool isCompilation))
            entity.IsCompilation = isCompilation;

        if (command.IsLock.TryGetValue(out bool isLock))
            entity.IsLock = isLock;

        if (command.LastFmUrl.TryGetValue(out string? lastFmUrl))
            entity.LastFmUrl = lastFmUrl;

        if (command.Biography.TryGetValue(out string? biography))
            entity.Biography = biography;

        if (command.MusicBrainzID.IsSet || command.ReleaseGroupMusicBrainzID.IsSet)
            entity.GetMetaDataLastAttempt = null;

        bool result = await _albumRepository.UpdateAsync(entity);

        if (result)
            return Result<bool>.Success(true);
        else
            return Result<bool>.Fail("Failed to update album.");
    }
}
