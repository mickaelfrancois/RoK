using Rok.Shared;

namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateAlbumEntity
{
    long Id { get; set; }

    PatchField<string>? Label { get; set; }

    PatchField<string>? Mood { get; set; }

    PatchField<string>? MusicBrainzID { get; set; }

    PatchField<DateTime?>? ReleaseDate { get; set; }

    PatchField<string>? ReleaseFormat { get; set; }

    PatchField<string>? Sales { get; set; }

    PatchField<string>? Speed { get; set; }

    PatchField<string>? Theme { get; set; }

    PatchField<string>? Wikipedia { get; set; }

    PatchField<bool>? IsLive { get; set; }

    PatchField<bool>? IsBestOf { get; set; }

    PatchField<bool>? IsCompilation { get; set; }
}