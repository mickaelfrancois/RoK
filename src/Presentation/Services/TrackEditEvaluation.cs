using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

/// <summary>Result of the rules applied to what is typed in the track edit dialog.</summary>
/// <param name="Title">Evaluation of the title.</param>
/// <param name="TrackNumber">Evaluation of the track number.</param>
/// <param name="Disc">Evaluation of the disc number.</param>
/// <param name="Composers">Evaluation of the composers.</param>
/// <param name="Bpm">Evaluation of the beats per minute.</param>
/// <param name="Edit">The changes to save; only the changed fields are set.</param>
public sealed record TrackEditEvaluation(TrackEditField Title, TrackEditField TrackNumber, TrackEditField Disc, TrackEditField Composers, TrackEditField Bpm, TrackTagsEdit Edit)
{
    /// <summary>Gets whether no field is invalid and at least one field changes.</summary>
    public bool CanSave
    {
        get
        {
            TrackEditField[] fields = [Title, TrackNumber, Disc, Composers, Bpm];

            return fields.All(item => item.State != TrackEditFieldState.Invalid) && fields.Any(item => item.State == TrackEditFieldState.Changed);
        }
    }
}