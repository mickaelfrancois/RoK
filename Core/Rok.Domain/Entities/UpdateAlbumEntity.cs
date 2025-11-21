using Rok.Shared;

namespace Rok.Domain.Entities;

public class UpdateAlbumEntity : IUpdateAlbumEntity
{
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
