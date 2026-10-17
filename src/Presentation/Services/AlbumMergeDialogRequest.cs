namespace Rok.Services;

/// <summary>What the album merge dialog starts with.</summary>
/// <param name="First">The album the user came from.</param>
/// <param name="Second">The other album when it is already known (a name collision), null when the user must pick it.</param>
/// <param name="KeepFirst">True when <paramref name="First"/> is the album that stays by default.</param>
/// <param name="Intro">A sentence that explains why the dialog opens, null for none.</param>
public sealed record AlbumMergeDialogRequest(AlbumMergeChoice First, AlbumMergeChoice? Second, bool KeepFirst, string? Intro);