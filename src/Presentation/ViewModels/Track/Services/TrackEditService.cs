using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Features.Tracks.Services;

namespace Rok.ViewModels.Track.Services;

/// <summary>Lets the user change the tags of a track and writes them through the mediator.</summary>
public class TrackEditService(IMediator mediator, IDialogService dialogService, IStringResourceProvider strings, ILogger<TrackEditService> logger)
{
    /// <summary>Opens the edit dialog and saves the typed values.</summary>
    /// <param name="current">The values the track has now.</param>
    /// <returns>The saved values, or <see langword="null"/> when the dialog was dismissed.</returns>
    public Task<TrackEditSaved?> EditAsync(TrackEditValues current)
    {
        return dialogService.ShowEditTrackAsync(current, TrySaveAsync);
    }

    /// <summary>Saves the changes and turns a refusal into a message.</summary>
    /// <param name="edit">The changed fields.</param>
    /// <returns>The saved values, or the translated message to show to the user.</returns>
    public async Task<TrackEditAttempt> TrySaveAsync(TrackTagsEdit edit)
    {
        try
        {
            Result<TrackEditSaved> result = await mediator.Send(new UpdateTrackTagsRequest(edit.TrackId, edit.Title, edit.TrackNumber, edit.Disc, edit.Composers, edit.Bpm));

            if (result.IsSuccess)
                return TrackEditAttempt.Success(result.Value);

            string? code = result.Errors.FirstOrDefault()?.Code;

            return TrackEditAttempt.Failure(GetMessage(code));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update the tags of track {TrackId}", edit.TrackId);

            return TrackEditAttempt.Failure(GetMessage(null));
        }
    }

    private string GetMessage(string? errorCode)
    {
        string message = strings.GetString(TrackEditTextKeys.Error(errorCode));

        if (!string.IsNullOrEmpty(message))
            return message;

        return strings.GetString(TrackEditTextKeys.GenericError);
    }
}