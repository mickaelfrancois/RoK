namespace Rok.Services;

/// <summary>Evaluation of one field of the track edit dialog.</summary>
/// <param name="State">State of the field.</param>
/// <param name="Preview">The text "old → new" when the field changes, otherwise null.</param>
/// <param name="ErrorKey">Resource key of the validation message when the field is invalid, otherwise null.</param>
public sealed record TrackEditField(TrackEditFieldState State, string? Preview = null, string? ErrorKey = null)
{
    /// <summary>The field keeps its current value.</summary>
    public static TrackEditField Unchanged { get; } = new(TrackEditFieldState.Unchanged);

    /// <summary>Builds a changed field.</summary>
    /// <param name="preview">The text "old → new".</param>
    /// <returns>The field.</returns>
    public static TrackEditField Changed(string preview) => new(TrackEditFieldState.Changed, preview);

    /// <summary>Builds an invalid field.</summary>
    /// <param name="errorKey">Resource key of the message.</param>
    /// <returns>The field.</returns>
    public static TrackEditField Invalid(string errorKey) => new(TrackEditFieldState.Invalid, ErrorKey: errorKey);
}