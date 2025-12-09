using Rok.Shared;

namespace Rok.Domain.Entities;

public class UpdateArtistEntity : IUpdateArtistEntity
{
    public long Id { get; set; }

    public PatchField<string>? Biography { get; set; }

    public PatchField<int>? BornYear { get; set; }

    public PatchField<int>? DiedYear { get; set; }

    public PatchField<bool>? Disbanded { get; set; }

    public PatchField<string>? FacebookUrl { get; set; }

    public PatchField<int>? FormedYear { get; set; }

    public PatchField<string>? Gender { get; set; }

    public PatchField<string>? Mood { get; set; }

    public PatchField<string>? MusicBrainzID { get; set; }

    public PatchField<string>? NovaUid { get; set; }

    public PatchField<string>? OfficialSiteUrl { get; set; }

    public PatchField<string>? Style { get; set; }

    public PatchField<string>? TwitterUrl { get; set; }

    public PatchField<string>? WikipediaUrl { get; set; }
}
