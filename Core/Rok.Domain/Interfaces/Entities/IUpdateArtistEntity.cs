using Rok.Shared;

namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateArtistEntity
{
    long Id { get; set; }

    PatchField<string>? Biography { get; set; }

    PatchField<int>? BornYear { get; set; }

    PatchField<int>? DiedYear { get; set; }

    PatchField<bool>? Disbanded { get; set; }

    PatchField<string>? FacebookUrl { get; set; }

    PatchField<int>? FormedYear { get; set; }

    PatchField<string>? Gender { get; set; }

    PatchField<string>? Mood { get; set; }

    PatchField<string>? MusicBrainzID { get; set; }

    PatchField<string>? NovaUid { get; set; }

    PatchField<string>? OfficialSiteUrl { get; set; }

    PatchField<string>? Style { get; set; }

    PatchField<string>? TwitterUrl { get; set; }

    PatchField<string>? WikipediaUrl { get; set; }
}