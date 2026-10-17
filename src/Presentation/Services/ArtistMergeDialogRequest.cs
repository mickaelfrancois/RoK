namespace Rok.Services;

/// <summary>What the merge dialog starts with.</summary>
/// <param name="First">The artist the user came from.</param>
/// <param name="Second">The other artist when it is already known (a name collision), null when the user must pick it.</param>
/// <param name="KeepFirst">True when <paramref name="First"/> is the artist that stays by default.</param>
/// <param name="Intro">A sentence that explains why the dialog opens, null for none.</param>
public sealed record ArtistMergeDialogRequest(ArtistMergeChoice First, ArtistMergeChoice? Second, bool KeepFirst, string? Intro);