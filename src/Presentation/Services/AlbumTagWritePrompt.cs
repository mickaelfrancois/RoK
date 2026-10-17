namespace Rok.Services;

/// <summary>What the album tag write dialog shows before it starts, already translated.</summary>
/// <param name="Changes">One line per field that changes, old value to new value.</param>
/// <param name="Notes">Counts and warnings that go with the changes.</param>
/// <param name="Blockers">One line per reason the write cannot start. Empty when it can.</param>
/// <param name="IsResume">True when the dialog finishes an interrupted write instead of starting an edit.</param>
public sealed record AlbumTagWritePrompt(IReadOnlyList<string> Changes, IReadOnlyList<string> Notes, IReadOnlyList<string> Blockers, bool IsResume)
{
    /// <summary>Gets a value indicating whether the write can start.</summary>
    public bool CanStart => Blockers.Count == 0;
}